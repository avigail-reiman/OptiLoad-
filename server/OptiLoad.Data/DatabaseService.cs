using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;
using OptiLoad.Core.Models;
using OptiLoad.Core.Services;

namespace OptiLoad.Data
{
    /// <summary>
    /// שכבת גישה למסד הנתונים (Data Access Layer).
    ///
    /// כל הגישה מתבצעת דרך Stored Procedures בלבד – ללא ORM.
    /// עובד עם ADO.NET ישיר עם SQL Server.
    ///
    /// עקרונות:
    ///   ‣ כל שאילתה דרך Stored Procedure (אבטחה, ביצועים)
    ///   ‣ שמירת תוצאות בטרנזקציה אחת (עקביות)
    ///   ‣ שגיאות נרשמות ב-ErrorLog ולא גורמות לקריסה
    /// </summary>
    public class DatabaseService : IPackingRepository
    {
        private readonly string _connectionString;

        public DatabaseService(string connectionString)
        {
            _connectionString = connectionString;
        }

        // ─────────────────────────────────────────────────────────────────
        // שליפת נתונים
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// שולף מידות מכולה עבור משימה ספציפית (SP_GetContainerTemplate).
        /// </summary>
        public async Task<ContainerDimensions> GetContainerDimensions(int jobId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand("SP_GetContainerTemplate", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@JobId", jobId);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                throw new InvalidOperationException($"לא נמצאה תבנית מכולה למשימה {jobId}");

            return new ContainerDimensions
            {
                Width       = reader.GetDouble(reader.GetOrdinal("Width")),
                Height      = reader.GetDouble(reader.GetOrdinal("Height")),
                Depth       = reader.GetDouble(reader.GetOrdinal("Depth")),
                MaxWeightKg = reader.GetDouble(reader.GetOrdinal("MaxWeightKg"))
            };
        }

        /// <summary>
        /// שולף רשימת ארגזים למשימה, כולל כמויות, וממיר ל-BoxInstance (SP_GetJobBoxes).
        /// </summary>
        public async Task<List<BoxInstance>> GetJobBoxInstances(int jobId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand("SP_GetJobBoxes", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@JobId", jobId);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            var instances = new List<BoxInstance>();

            while (await reader.ReadAsync())
            {
                var box = new Box
                {
                    BoxId         = reader.GetInt32(reader.GetOrdinal("BoxId")),
                    BoxName       = reader.GetString(reader.GetOrdinal("BoxName")),
                    Width         = reader.GetDouble(reader.GetOrdinal("Width")),
                    Height        = reader.GetDouble(reader.GetOrdinal("Height")),
                    Depth         = reader.GetDouble(reader.GetOrdinal("Depth")),
                    WeightKg      = reader.GetDouble(reader.GetOrdinal("WeightKg")),
                    IsFragile     = reader.GetBoolean(reader.GetOrdinal("IsFragile")),
                    AllowRotation = reader.GetBoolean(reader.GetOrdinal("AllowRotation"))
                };

                int quantity = reader.GetInt32(reader.GetOrdinal("Quantity"));

                for (int i = 0; i < quantity; i++)
                {
                    instances.Add(new BoxInstance
                    {
                        BoxDefinition = box,
                        InstanceIndex = i + 1
                    });
                }
            }

            return instances;
        }

        // ─────────────────────────────────────────────────────────────────
        // שמירת תוצאות
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// שומר את כל מיקומי הארגזים בטרנזקציה אחת (SP_SavePlacementResults).
        /// אם אחד נכשל – הכל נמחק (Rollback).
        /// </summary>
        public async Task SavePlacementResults(int jobId, PackingResult result)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                foreach (var placed in result.PlacedBoxes)
                {
                    using var cmd = new SqlCommand(
                        "INSERT INTO PlacementResult " +
                        "(JobId,BoxId,InstanceIndex,BinIndex,PosX,PosY,PosZ," +
                        "PlacedWidth,PlacedHeight,PlacedDepth,RotationIndex,CreatedAt) " +
                        "VALUES (@JobId,@BoxId,@InstanceIndex,@BinIndex,@PosX,@PosY,@PosZ," +
                        "@PlacedWidth,@PlacedHeight,@PlacedDepth,@RotationIndex,@CreatedAt);",
                        conn, transaction)
                    {
                        CommandType = CommandType.Text
                    };

                    cmd.Parameters.AddWithValue("@JobId",         jobId);
                    cmd.Parameters.AddWithValue("@BoxId",         placed.Instance.BoxDefinition.BoxId);
                    cmd.Parameters.AddWithValue("@InstanceIndex", placed.Instance.InstanceIndex);
                    cmd.Parameters.AddWithValue("@BinIndex",      placed.BinIndex);
                    cmd.Parameters.AddWithValue("@PosX",          placed.Position.X);
                    cmd.Parameters.AddWithValue("@PosY",          placed.Position.Y);
                    cmd.Parameters.AddWithValue("@PosZ",          placed.Position.Z);
                    cmd.Parameters.AddWithValue("@PlacedWidth",   placed.Rotation.W);
                    cmd.Parameters.AddWithValue("@PlacedHeight",  placed.Rotation.H);
                    cmd.Parameters.AddWithValue("@PlacedDepth",   placed.Rotation.D);
                    cmd.Parameters.AddWithValue("@RotationIndex", placed.Rotation.Index);
                    cmd.Parameters.AddWithValue("@CreatedAt",     DateTime.UtcNow);

                    await cmd.ExecuteNonQueryAsync();
                }

                transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                await LogError(jobId, "SavePlacementResults", ex);
                throw;
            }
        }

        /// <summary>
        /// מעדכנת סטטוס, ניצול נפח, משקל וזמן פתרון בסיום ריצה (SP_CompleteJob).
        /// </summary>
        public async Task CompleteJob(int jobId, PackingResult result)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand(
                "UPDATE PackingJob SET " +
                "Status='Completed', BinsUsed=@BinsUsed, VolumeUtilization=@VolumeUtilization, " +
                "TotalWeightKg=@TotalWeightKg, SolveTimeSeconds=@SolveTimeSeconds, " +
                "IsOptimal=@IsOptimal, StatusMessage=@StatusMessage, CompletedAt=@CompletedAt " +
                "WHERE JobId=@JobId", conn)
            {
                CommandType = CommandType.Text
            };

            cmd.Parameters.AddWithValue("@JobId",             jobId);
            cmd.Parameters.AddWithValue("@BinsUsed",          result.BinsUsed);
            cmd.Parameters.AddWithValue("@VolumeUtilization", result.VolumeUtilization);
            cmd.Parameters.AddWithValue("@TotalWeightKg",
                result.PlacedBoxes.Count > 0
                    ? result.PlacedBoxes.Sum(p => p.Instance.BoxDefinition.WeightKg)
                    : 0.0);
            cmd.Parameters.AddWithValue("@SolveTimeSeconds",  result.SolveTime.TotalSeconds);
            cmd.Parameters.AddWithValue("@IsOptimal",         result.IsOptimal);
            cmd.Parameters.AddWithValue("@StatusMessage",     result.StatusMessage);
            cmd.Parameters.AddWithValue("@CompletedAt",       DateTime.UtcNow);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        // ─────────────────────────────────────────────────────────────────
        // יצירת משימה
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// יוצר משימת שיבוץ חדשה ומחזיר JobId (SP_CreatePackingJob).
        /// </summary>
        public async Task<int> CreatePackingJob(int containerId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand(
                "INSERT INTO PackingJob (ContainerId, Status, CreatedAt) " +
                "VALUES (@ContainerId, 'Pending', @CreatedAt); " +
                "SELECT SCOPE_IDENTITY();", conn)
            {
                CommandType = CommandType.Text
            };
            cmd.Parameters.AddWithValue("@ContainerId", containerId);
            cmd.Parameters.AddWithValue("@CreatedAt",   DateTime.UtcNow);

            await conn.OpenAsync();
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        /// <summary>
        /// מוסיף ארגז למשימה (SP_AddBoxesToJob).
        /// </summary>
        public async Task AddBoxToJob(int jobId, int boxId, int quantity)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand(
                "INSERT INTO PackingJobBox (JobId, BoxId, Quantity) " +
                "VALUES (@JobId, @BoxId, @Quantity);", conn)
            {
                CommandType = CommandType.Text
            };
            cmd.Parameters.AddWithValue("@JobId",    jobId);
            cmd.Parameters.AddWithValue("@BoxId",    boxId);
            cmd.Parameters.AddWithValue("@Quantity", quantity);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        // ─────────────────────────────────────────────────────────────────
        // רישום שגיאות
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// רושם שגיאה ב-ErrorLog – בולע exceptions כדי למנוע קריסה.
        /// </summary>
        public async Task LogError(int jobId, string context, Exception ex)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                using var cmd  = new SqlCommand(
                    "INSERT INTO ErrorLog (JobId, Context, Message, StackTrace, CreatedAt) " +
                    "VALUES (@JobId, @Context, @Message, @StackTrace, @CreatedAt);", conn)
                {
                    CommandType = CommandType.Text
                };

                cmd.Parameters.AddWithValue("@JobId",      (object?)jobId == null ? DBNull.Value : jobId);
                cmd.Parameters.AddWithValue("@Context",    context);
                cmd.Parameters.AddWithValue("@Message",    ex.Message);
                cmd.Parameters.AddWithValue("@StackTrace", ex.StackTrace ?? string.Empty);
                cmd.Parameters.AddWithValue("@CreatedAt",  DateTime.UtcNow);

                await conn.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
            catch
            {
                // בלע – לא להקריס בגלל כשל ברישום שגיאות
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // CRUD – Box
        // ─────────────────────────────────────────────────────────────────

        public async Task<List<Box>> GetAllBoxes()
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand("SELECT BoxId, BoxName, Width, Height, Depth, WeightKg, IsFragile, AllowRotation, CreatedAt FROM Box ORDER BY BoxId", conn)
            {
                CommandType = CommandType.Text
            };
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            var list = new List<Box>();
            while (await reader.ReadAsync())
            {
                list.Add(new Box
                {
                    BoxId         = reader.GetInt32(0),
                    BoxName       = reader.GetString(1),
                    Width         = reader.GetDouble(2),
                    Height        = reader.GetDouble(3),
                    Depth         = reader.GetDouble(4),
                    WeightKg      = reader.GetDouble(5),
                    IsFragile     = reader.GetBoolean(6),
                    AllowRotation = reader.GetBoolean(7),
                    CreatedAt     = reader.GetDateTime(8)
                });
            }
            return list;
        }

        public async Task<Box?> GetBoxById(int boxId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand("SELECT BoxId, BoxName, Width, Height, Depth, WeightKg, IsFragile, AllowRotation, CreatedAt FROM Box WHERE BoxId=@BoxId", conn)
            {
                CommandType = CommandType.Text
            };
            cmd.Parameters.AddWithValue("@BoxId", boxId);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;
            return new Box
            {
                BoxId         = reader.GetInt32(0),
                BoxName       = reader.GetString(1),
                Width         = reader.GetDouble(2),
                Height        = reader.GetDouble(3),
                Depth         = reader.GetDouble(4),
                WeightKg      = reader.GetDouble(5),
                IsFragile     = reader.GetBoolean(6),
                AllowRotation = reader.GetBoolean(7),
                CreatedAt     = reader.GetDateTime(8)
            };
        }

        public async Task<int> CreateBox(Box box)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand(
                "INSERT INTO Box (BoxName,Width,Height,Depth,WeightKg,IsFragile,AllowRotation,CreatedAt) " +
                "VALUES (@BoxName,@Width,@Height,@Depth,@WeightKg,@IsFragile,@AllowRotation,@CreatedAt); " +
                "SELECT SCOPE_IDENTITY();", conn)
            {
                CommandType = CommandType.Text
            };
            cmd.Parameters.AddWithValue("@BoxName",       box.BoxName);
            cmd.Parameters.AddWithValue("@Width",         box.Width);
            cmd.Parameters.AddWithValue("@Height",        box.Height);
            cmd.Parameters.AddWithValue("@Depth",         box.Depth);
            cmd.Parameters.AddWithValue("@WeightKg",      box.WeightKg);
            cmd.Parameters.AddWithValue("@IsFragile",     box.IsFragile);
            cmd.Parameters.AddWithValue("@AllowRotation", box.AllowRotation);
            cmd.Parameters.AddWithValue("@CreatedAt",     box.CreatedAt == default ? DateTime.UtcNow : box.CreatedAt);
            await conn.OpenAsync();
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        public async Task UpdateBox(int boxId, Box box)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand(
                "UPDATE Box SET BoxName=@BoxName,Width=@Width,Height=@Height,Depth=@Depth," +
                "WeightKg=@WeightKg,IsFragile=@IsFragile,AllowRotation=@AllowRotation " +
                "WHERE BoxId=@BoxId", conn)
            { CommandType = CommandType.Text };
            cmd.Parameters.AddWithValue("@BoxName",       box.BoxName);
            cmd.Parameters.AddWithValue("@Width",         box.Width);
            cmd.Parameters.AddWithValue("@Height",        box.Height);
            cmd.Parameters.AddWithValue("@Depth",         box.Depth);
            cmd.Parameters.AddWithValue("@WeightKg",      box.WeightKg);
            cmd.Parameters.AddWithValue("@IsFragile",     box.IsFragile);
            cmd.Parameters.AddWithValue("@AllowRotation", box.AllowRotation);
            cmd.Parameters.AddWithValue("@BoxId",         boxId);
            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<bool> DeleteBox(int boxId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand("DELETE FROM Box WHERE BoxId=@BoxId", conn)
            { CommandType = CommandType.Text };
            cmd.Parameters.AddWithValue("@BoxId", boxId);
            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        // ─────────────────────────────────────────────────────────────────
        // CRUD – ContainerTemplate + Container
        // ─────────────────────────────────────────────────────────────────

        public async Task<List<ContainerTemplate>> GetAllContainerTemplates()
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand("SELECT TemplateId, TemplateName, Width, Height, Depth, MaxWeightKg, CreatedAt FROM ContainerTemplate ORDER BY TemplateId", conn)
            {
                CommandType = CommandType.Text
            };
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            var list = new List<ContainerTemplate>();
            while (await reader.ReadAsync())
            {
                list.Add(new ContainerTemplate
                {
                    TemplateId   = reader.GetInt32(0),
                    TemplateName = reader.GetString(1),
                    Width        = reader.GetDouble(2),
                    Height       = reader.GetDouble(3),
                    Depth        = reader.GetDouble(4),
                    MaxWeightKg  = reader.GetDouble(5),
                    CreatedAt    = reader.GetDateTime(6)
                });
            }
            return list;
        }

        public async Task<ContainerTemplate?> GetContainerTemplateById(int templateId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand("SELECT TemplateId, TemplateName, Width, Height, Depth, MaxWeightKg, CreatedAt FROM ContainerTemplate WHERE TemplateId=@Id", conn)
            {
                CommandType = CommandType.Text
            };
            cmd.Parameters.AddWithValue("@Id", templateId);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;
            return new ContainerTemplate
            {
                TemplateId   = reader.GetInt32(0),
                TemplateName = reader.GetString(1),
                Width        = reader.GetDouble(2),
                Height       = reader.GetDouble(3),
                Depth        = reader.GetDouble(4),
                MaxWeightKg  = reader.GetDouble(5),
                CreatedAt    = reader.GetDateTime(6)
            };
        }

        public async Task<int> CreateContainerTemplate(ContainerTemplate t)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand(
                "INSERT INTO ContainerTemplate (TemplateName,Width,Height,Depth,MaxWeightKg,CreatedAt) " +
                "VALUES (@Name,@W,@H,@D,@MW,@Cat); SELECT SCOPE_IDENTITY();", conn)
            { CommandType = CommandType.Text };
            cmd.Parameters.AddWithValue("@Name", t.TemplateName);
            cmd.Parameters.AddWithValue("@W",    t.Width);
            cmd.Parameters.AddWithValue("@H",    t.Height);
            cmd.Parameters.AddWithValue("@D",    t.Depth);
            cmd.Parameters.AddWithValue("@MW",   t.MaxWeightKg);
            cmd.Parameters.AddWithValue("@Cat",  DateTime.UtcNow);
            await conn.OpenAsync();
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        public async Task<List<Container>> GetAllContainers()
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand("SELECT ContainerId, TemplateId, ContainerCode, Status, CreatedAt FROM Container ORDER BY ContainerId", conn)
            {
                CommandType = CommandType.Text
            };
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            var list = new List<Container>();
            while (await reader.ReadAsync())
            {
                list.Add(new Container
                {
                    ContainerId   = reader.GetInt32(0),
                    TemplateId    = reader.GetInt32(1),
                    ContainerCode = reader.GetString(2),
                    Status        = Enum.Parse<ContainerStatus>(reader.GetString(3)),
                    CreatedAt     = reader.GetDateTime(4)
                });
            }
            return list;
        }

        // ─────────────────────────────────────────────────────────────────
        // CRUD – PackingJob
        // ─────────────────────────────────────────────────────────────────

        public async Task<List<PackingJob>> GetAllJobs()
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand("SELECT JobId, ContainerId, Status, BinsUsed, VolumeUtilization, TotalWeightKg, SolveTimeSeconds, IsOptimal, StatusMessage, CreatedAt, CompletedAt FROM PackingJob ORDER BY JobId DESC", conn)
            {
                CommandType = CommandType.Text
            };
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            var list = new List<PackingJob>();
            while (await reader.ReadAsync())
                list.Add(ReadJob(reader));
            return list;
        }

        public async Task<PackingJob?> GetJobById(int jobId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand("SELECT JobId, ContainerId, Status, BinsUsed, VolumeUtilization, TotalWeightKg, SolveTimeSeconds, IsOptimal, StatusMessage, CreatedAt, CompletedAt FROM PackingJob WHERE JobId=@JobId", conn)
            {
                CommandType = CommandType.Text
            };
            cmd.Parameters.AddWithValue("@JobId", jobId);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;
            return ReadJob(reader);
        }

        private static PackingJob ReadJob(SqlDataReader reader) => new()
        {
            JobId             = reader.GetInt32(0),
            ContainerId       = reader.GetInt32(1),
            Status            = Enum.Parse<JobStatus>(reader.GetString(2)),
            BinsUsed          = reader.IsDBNull(3)  ? null : reader.GetInt32(3),
            VolumeUtilization = reader.IsDBNull(4)  ? null : reader.GetDouble(4),
            TotalWeightKg     = reader.IsDBNull(5)  ? null : reader.GetDouble(5),
            SolveTimeSeconds  = reader.IsDBNull(6)  ? null : reader.GetDouble(6),
            IsOptimal         = reader.IsDBNull(7)  ? null : reader.GetBoolean(7),
            StatusMessage     = reader.IsDBNull(8)  ? null : reader.GetString(8),
            CreatedAt         = reader.GetDateTime(9),
            CompletedAt       = reader.IsDBNull(10) ? null : reader.GetDateTime(10)
        };

        // ─────────────────────────────────────────────────────────────────
        // PlacementResult
        // ─────────────────────────────────────────────────────────────────

        public async Task<bool> DeleteJob(int jobId)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // מחק בסדר: PlacementResult → PackingJobBox → PackingJob (FK constraints)
            foreach (var sql in new[]
            {
                "DELETE FROM PlacementResult WHERE JobId=@JobId",
                "DELETE FROM PackingJobBox   WHERE JobId=@JobId",
                "DELETE FROM PackingJob      WHERE JobId=@JobId"
            })
            {
                using var cmd = new SqlCommand(sql, conn) { CommandType = CommandType.Text };
                cmd.Parameters.AddWithValue("@JobId", jobId);
                await cmd.ExecuteNonQueryAsync();
            }

            return true;
        }

        public async Task<int> CreateContainer(int templateId, string code)
        {            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand(
                "INSERT INTO Container (TemplateId, ContainerCode, Status, CreatedAt) VALUES (@TemplateId, @Code, 'Available', @CreatedAt); SELECT SCOPE_IDENTITY();", conn)
            {
                CommandType = System.Data.CommandType.Text
            };
            cmd.Parameters.AddWithValue("@TemplateId", templateId);
            cmd.Parameters.AddWithValue("@Code",       code);
            cmd.Parameters.AddWithValue("@CreatedAt",  DateTime.UtcNow);
            await conn.OpenAsync();
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        public async Task<List<PlacementResult>> GetPlacementResults(int jobId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand(
                "SELECT pr.PlacementId, pr.JobId, pr.BoxId, pr.InstanceIndex, pr.BinIndex, pr.PosX, pr.PosY, pr.PosZ, pr.PlacedWidth, pr.PlacedHeight, pr.PlacedDepth, pr.RotationIndex, pr.CreatedAt, b.BoxName, b.IsFragile " +
                "FROM PlacementResult pr JOIN Box b ON b.BoxId = pr.BoxId WHERE pr.JobId=@JobId ORDER BY pr.BinIndex, pr.PlacementId", conn)
            {
                CommandType = CommandType.Text
            };
            cmd.Parameters.AddWithValue("@JobId", jobId);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            var list = new List<PlacementResult>();
            while (await reader.ReadAsync())
            {
                list.Add(new PlacementResult
                {
                    PlacementId   = reader.GetInt32(0),
                    JobId         = reader.GetInt32(1),
                    BoxId         = reader.GetInt32(2),
                    InstanceIndex = reader.GetInt32(3),
                    BinIndex      = reader.GetInt32(4),
                    PosX          = reader.GetDouble(5),
                    PosY          = reader.GetDouble(6),
                    PosZ          = reader.GetDouble(7),
                    PlacedWidth   = reader.GetDouble(8),
                    PlacedHeight  = reader.GetDouble(9),
                    PlacedDepth   = reader.GetDouble(10),
                    RotationIndex = reader.GetInt32(11),
                    CreatedAt     = reader.GetDateTime(12),
                    BoxName       = reader.GetString(13),
                    IsFragile     = reader.GetBoolean(14)
                });
            }
            return list;
        }

        // ─────────────────────────────────────────────────────────────────
        // Upsert – Box + ContainerTemplate
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// מוצא ארגז לפי שם → מעדכן את מידותיו ומחזיר BoxId הקיים.
        /// אם לא קיים → מכניס ומחזיר BoxId חדש.
        /// </summary>
        public async Task<int> UpsertBox(Box box)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // בדוק אם קיים
            using var check = new SqlCommand(
                "SELECT BoxId FROM Box WHERE BoxName = @BoxName", conn)
            { CommandType = CommandType.Text };
            check.Parameters.AddWithValue("@BoxName", box.BoxName);
            var existing = await check.ExecuteScalarAsync();

            if (existing != null)
            {
                // עדכן
                int id = Convert.ToInt32(existing);
                using var upd = new SqlCommand(
                    "UPDATE Box SET Width=@Width,Height=@Height,Depth=@Depth," +
                    "WeightKg=@WeightKg,IsFragile=@IsFragile,AllowRotation=@AllowRotation " +
                    "WHERE BoxId=@BoxId", conn)
                { CommandType = CommandType.Text };
                upd.Parameters.AddWithValue("@Width",         box.Width);
                upd.Parameters.AddWithValue("@Height",        box.Height);
                upd.Parameters.AddWithValue("@Depth",         box.Depth);
                upd.Parameters.AddWithValue("@WeightKg",      box.WeightKg);
                upd.Parameters.AddWithValue("@IsFragile",     box.IsFragile);
                upd.Parameters.AddWithValue("@AllowRotation", box.AllowRotation);
                upd.Parameters.AddWithValue("@BoxId",         id);
                await upd.ExecuteNonQueryAsync();
                return id;
            }
            else
            {
                // הכנס חדש
                using var ins = new SqlCommand(
                    "INSERT INTO Box (BoxName,Width,Height,Depth,WeightKg,IsFragile,AllowRotation,CreatedAt) " +
                    "VALUES (@BoxName,@Width,@Height,@Depth,@WeightKg,@IsFragile,@AllowRotation,@CreatedAt); " +
                    "SELECT SCOPE_IDENTITY();", conn)
                { CommandType = CommandType.Text };
                ins.Parameters.AddWithValue("@BoxName",       box.BoxName);
                ins.Parameters.AddWithValue("@Width",         box.Width);
                ins.Parameters.AddWithValue("@Height",        box.Height);
                ins.Parameters.AddWithValue("@Depth",         box.Depth);
                ins.Parameters.AddWithValue("@WeightKg",      box.WeightKg);
                ins.Parameters.AddWithValue("@IsFragile",     box.IsFragile);
                ins.Parameters.AddWithValue("@AllowRotation", box.AllowRotation);
                ins.Parameters.AddWithValue("@CreatedAt",     DateTime.UtcNow);
                return Convert.ToInt32(await ins.ExecuteScalarAsync());
            }
        }

        /// <summary>
        /// מוצא תבנית מכולה לפי מידות (סבילות 0.01) → מחזיר TemplateId קיים.
        /// אם לא קיים → מכניס תבנית חדשה ומחזיר TemplateId חדש.
        /// </summary>
        public async Task<int> UpsertContainerTemplate(ContainerDimensions dims)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            using var check = new SqlCommand(
                "SELECT TOP 1 TemplateId FROM ContainerTemplate " +
                "WHERE ABS(Width-@W)<0.01 AND ABS(Height-@H)<0.01 " +
                "AND ABS(Depth-@D)<0.01 AND ABS(MaxWeightKg-@MW)<0.01", conn)
            { CommandType = CommandType.Text };
            check.Parameters.AddWithValue("@W",  dims.Width);
            check.Parameters.AddWithValue("@H",  dims.Height);
            check.Parameters.AddWithValue("@D",  dims.Depth);
            check.Parameters.AddWithValue("@MW", dims.MaxWeightKg);
            var existing = await check.ExecuteScalarAsync();

            if (existing != null)
                return Convert.ToInt32(existing);

            using var ins = new SqlCommand(
                "INSERT INTO ContainerTemplate (TemplateName,Width,Height,Depth,MaxWeightKg,CreatedAt) " +
                "VALUES (@Name,@W,@H,@D,@MW,@Cat); SELECT SCOPE_IDENTITY();", conn)
            { CommandType = CommandType.Text };
            ins.Parameters.AddWithValue("@Name", $"{dims.Width}x{dims.Height}x{dims.Depth}");
            ins.Parameters.AddWithValue("@W",    dims.Width);
            ins.Parameters.AddWithValue("@H",    dims.Height);
            ins.Parameters.AddWithValue("@D",    dims.Depth);
            ins.Parameters.AddWithValue("@MW",   dims.MaxWeightKg);
            ins.Parameters.AddWithValue("@Cat",  DateTime.UtcNow);
            return Convert.ToInt32(await ins.ExecuteScalarAsync());
        }
    }
}
