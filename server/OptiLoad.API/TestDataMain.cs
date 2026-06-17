// ⚠️ DEAD CODE — קובץ זה לא נקרא לעולם. נקודת הכניסה של הפרויקט היא Program.cs
using System.Threading.Tasks;
using OptiLoad.Core.Services;

class TestDataMain
{
    public static async Task Main(string[] args)
    {
        await TestDataRunner.RunFromJson("../OptiLoad.Core/TestData/SampleTestData.json");
    }
}
