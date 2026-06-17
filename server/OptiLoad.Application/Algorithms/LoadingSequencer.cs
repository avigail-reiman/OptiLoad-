using System;
using System.Collections.Generic;
using System.Linq;
using OptiLoad.Application.Algorithms;
using OptiLoad.Core.Models;

namespace OptiLoad.Application.Algorithms;

public enum LoadingFace { Front, Back, Left, Right }

public static class LoadingSequencer
{
    private const double TouchEps = 0.5;

    public static IReadOnlyList<(PlacedBox Box, int LoadingStep)> Sequence(
        IReadOnlyList<PlacedBox> boxes,
        LoadingFace face = LoadingFace.Front)
    {
        int n = boxes.Count;
        if (n == 0) return Array.Empty<(PlacedBox, int)>();

        var idx      = Enumerable.Range(0, n).ToList();
        var inDegree = new int[n];
        var adjList  = new List<int>[n];
        for (int i = 0; i < n; i++) adjList[i] = new List<int>();

        var supportEdges = new HashSet<(int, int)>();
        for (int a = 0; a < n; a++)
        for (int b = 0; b < n; b++)
        {
            if (a != b && boxes[a].BinIndex == boxes[b].BinIndex)
            {
                if (IsBelow(boxes[a], boxes[b])) supportEdges.Add((a, b));
            }
        }

        for (int a = 0; a < n; a++)
        for (int b = 0; b < n; b++)
        {
            if (a != b)
            {
                var A = boxes[a]; var B = boxes[b];
                if (A.BinIndex == B.BinIndex)
                {
                    bool addEdge = supportEdges.Contains((a, b))
                                || (IsBlocking(A, B, face) && !supportEdges.Contains((b, a)));

                    if (addEdge)
                    {
                        adjList[a].Add(b);
                        inDegree[b]++;
                    }
                }
            }
        }

        var queue = new SortedSet<int>(
            idx.Where(i => inDegree[i] == 0),
            Comparer<int>.Create((a, b) =>
            {
                int cb = boxes[a].BinIndex.CompareTo(boxes[b].BinIndex);
                if (cb != 0) return cb;
                int cd = DepthCompare(boxes[a], boxes[b], face);
                if (cd != 0) return cd;
                int cy = boxes[a].Y1.CompareTo(boxes[b].Y1);
                if (cy != 0) return cy;
                return a.CompareTo(b);
            }));

        var result  = new List<(PlacedBox, int)>(n);
        int step    = 0;
        int visited = 0;

        while (queue.Count > 0)
        {
            int cur = queue.Min;
            queue.Remove(cur);
            result.Add((boxes[cur], step++));
            visited++;

            foreach (int next in adjList[cur])
            {
                inDegree[next]--;
                if (inDegree[next] == 0)
                    queue.Add(next);
            }
        }

        if (visited < n)
            for (int i = 0; i < n; i++)
                if (inDegree[i] > 0)
                    result.Add((boxes[i], step++));

        return result;
    }

    private static int DepthCompare(PlacedBox a, PlacedBox b, LoadingFace face) => face switch
    {
        LoadingFace.Front => b.Z1.CompareTo(a.Z1),
        LoadingFace.Back  => a.Z1.CompareTo(b.Z1),
        LoadingFace.Left  => b.X1.CompareTo(a.X1),
        LoadingFace.Right => a.X1.CompareTo(b.X1),
        _                 => b.Z1.CompareTo(a.Z1)
    };

    private static bool IsBlocking(PlacedBox a, PlacedBox b, LoadingFace face)
    {
        bool aIsDeeper = face switch
        {
            LoadingFace.Front => a.Z1 > b.Z1 + AlgorithmConfig.LayerEpsilon,
            LoadingFace.Back  => a.Z1 < b.Z1 - AlgorithmConfig.LayerEpsilon,
            LoadingFace.Left  => a.X1 > b.X1 + AlgorithmConfig.LayerEpsilon,
            LoadingFace.Right => a.X1 < b.X1 - AlgorithmConfig.LayerEpsilon,
            _                 => a.Z1 > b.Z1 + AlgorithmConfig.LayerEpsilon
        };

        bool overlapDepthAxis, overlapY;
        overlapY = a.Y1 < b.Y2 - AlgorithmConfig.LayerEpsilon && b.Y1 < a.Y2 - AlgorithmConfig.LayerEpsilon;

        if (face == LoadingFace.Front || face == LoadingFace.Back)
            overlapDepthAxis = a.X1 < b.X2 - AlgorithmConfig.LayerEpsilon && b.X1 < a.X2 - AlgorithmConfig.LayerEpsilon;
        else
            overlapDepthAxis = a.Z1 < b.Z2 - AlgorithmConfig.LayerEpsilon && b.Z1 < a.Z2 - AlgorithmConfig.LayerEpsilon;

        return aIsDeeper && overlapDepthAxis && overlapY;
    }

    private static bool IsBelow(PlacedBox a, PlacedBox b)
    {
        bool topTouchesBase = Math.Abs(a.Y2 - b.Y1) < TouchEps;
        bool overlapX = a.X1 < b.X2 - AlgorithmConfig.LayerEpsilon && b.X1 < a.X2 - AlgorithmConfig.LayerEpsilon;
        bool overlapZ = a.Z1 < b.Z2 - AlgorithmConfig.LayerEpsilon && b.Z1 < a.Z2 - AlgorithmConfig.LayerEpsilon;
        return topTouchesBase && overlapX && overlapZ;
    }
}


