using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RouteFinder.Tests
{
    [TestClass]
    public class RouteFinderTests
    {
        private string? _testFilePath;

        [TestInitialize]
        public void Setup()
        {
            _testFilePath = Path.GetTempFileName();
            File.WriteAllText(_testFilePath,
                "1 2 2.5\n" +
                "2 3 1.8\n" +
                "1 4 3.0\n" +
                "2 5 2.2\n" +
                "3 6 1.5\n" +
                "4 5 2.1\n" +
                "5 6 1.9\n");
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (_testFilePath != null && File.Exists(_testFilePath))
                File.Delete(_testFilePath);
        }

        [TestMethod]
        public void FindBestRoute_ThreePoints_ReturnsCorrectRouteAndDistance()
        {
            Assert.IsNotNull(_testFilePath);

            int[] points = new int[] { 1, 3, 4 };

            var (graph, nodeCount) = RouteFinderHelper.LoadFromFile(_testFilePath);
            var (distances, next) = RouteFinderHelper.FloydWithPath(graph, nodeCount);
            var result = RouteFinderHelper.FindBestRouteWithPath(points, distances, next);

            Assert.IsFalse(double.IsInfinity(result.Distance));
            Assert.AreEqual(7.3, Math.Round(result.Distance, 1));

            Assert.IsNotNull(result.Route);

            bool validRoute1 = result.Route == "3 -> 2 -> 1 -> 4";
            bool validRoute2 = result.Route == "4 -> 1 -> 2 -> 3";

            Assert.IsTrue(validRoute1 || validRoute2);
        }

        [TestMethod]
        public void FloydAlgorithm_CorrectlyComputesShortestPaths()
        {
            Assert.IsNotNull(_testFilePath);

            var (graph, nodeCount) = RouteFinderHelper.LoadFromFile(_testFilePath);
            var (distances, next) = RouteFinderHelper.FloydWithPath(graph, nodeCount);

            Assert.AreEqual(2.5, distances[0, 1], 0.001);
            Assert.AreEqual(4.3, distances[0, 2], 0.001);
            Assert.AreEqual(3.0, distances[0, 3], 0.001);
            Assert.AreEqual(4.7, distances[0, 4], 0.001);
            Assert.AreEqual(5.8, distances[0, 5], 0.001);
        }

        [TestMethod]
        public void LoadFromFile_ValidFile_ReturnsCorrectGraph()
        {
            Assert.IsNotNull(_testFilePath);

            var (graph, nodeCount) = RouteFinderHelper.LoadFromFile(_testFilePath);

            Assert.AreEqual(6, nodeCount);
            Assert.AreEqual(2.5, graph[0, 1], 0.001);
            Assert.AreEqual(1.8, graph[1, 2], 0.001);
            Assert.AreEqual(3.0, graph[0, 3], 0.001);
            Assert.AreEqual(double.PositiveInfinity, graph[0, 5]);
        }
    }

    public static class RouteFinderHelper
    {
        public static (double[,] graph, int nodeCount) LoadFromFile(string fileName)
        {
            if (!File.Exists(fileName))
                throw new FileNotFoundException($"Файл {fileName} не найден");

            string[] lines = File.ReadAllLines(fileName);
            var edges = new List<(int from, int to, double distance)>();
            int maxNode = 0;

            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();

                if (string.IsNullOrWhiteSpace(trimmedLine))
                    continue;

                string[] parts = trimmedLine.Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length < 3)
                    continue;

                if (int.TryParse(parts[0], out int fromNode) &&
                    int.TryParse(parts[1], out int toNode))
                {
                    double distance;
                    if (!double.TryParse(parts[2].Replace('.', ','), out distance) &&
                        !double.TryParse(parts[2].Replace(',', '.'), out distance))
                    {
                        continue;
                    }

                    edges.Add((fromNode, toNode, distance));
                    maxNode = Math.Max(maxNode, Math.Max(fromNode, toNode));
                }
            }

            if (edges.Count == 0)
                throw new InvalidDataException("В файле не найдено корректных данных о рёбрах");

            double[,] graph = new double[maxNode, maxNode];
            for (int i = 0; i < maxNode; i++)
                for (int j = 0; j < maxNode; j++)
                    graph[i, j] = (i == j) ? 0 : double.PositiveInfinity;

            foreach (var edge in edges)
            {
                graph[edge.from - 1, edge.to - 1] = edge.distance;
                graph[edge.to - 1, edge.from - 1] = edge.distance;
            }

            return (graph, maxNode);
        }

        public static (double[,] dist, int[,] next) FloydWithPath(double[,] a, int n)
        {
            double[,] dist = (double[,])a.Clone();
            int[,] next = new int[n, n];

            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    if (!double.IsInfinity(dist[i, j]) && i != j)
                        next[i, j] = j;
                    else
                        next[i, j] = -1;

            for (int k = 0; k < n; k++)
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < n; j++)
                        if (dist[i, k] + dist[k, j] < dist[i, j] - 0.0001)
                        {
                            dist[i, j] = dist[i, k] + dist[k, j];
                            next[i, j] = next[i, k];
                        }

            return (dist, next);
        }

        public static List<int> GetPath(int from, int to, int[,] next)
        {
            var path = new List<int>();

            if (next[from, to] == -1)
                return path;

            path.Add(from + 1);

            int current = from;
            while (current != to)
            {
                current = next[current, to];
                path.Add(current + 1);
            }

            return path;
        }

        public static (string Route, double Distance) FindBestRouteWithPath(int[] points, double[,] distances, int[,] next)
        {
            if (points == null || points.Length != 3)
                return ("Некорректные точки", double.PositiveInfinity);

            foreach (int p1 in points)
                foreach (int p2 in points)
                    if (p1 != p2 && double.IsInfinity(distances[p1 - 1, p2 - 1]))
                        return ($"Нет пути между точками {p1} и {p2}", double.PositiveInfinity);

            var permutations = GetPermutations(points, 3);

            double minDistance = double.PositiveInfinity;
            List<int>? bestFullRoute = null;

            foreach (var perm in permutations)
            {
                int[] route = perm.ToArray();

                var path1 = GetPath(route[0] - 1, route[1] - 1, next);
                var path2 = GetPath(route[1] - 1, route[2] - 1, next);

                if (path1.Count == 0 || path2.Count == 0)
                    continue;

                var fullRoute = new List<int>();
                fullRoute.AddRange(path1);

                if (path2.Count > 1)
                    fullRoute.AddRange(path2.Skip(1));

                double distance = distances[route[0] - 1, route[1] - 1] +
                                 distances[route[1] - 1, route[2] - 1];

                if (distance < minDistance && distance > 0)
                {
                    minDistance = distance;
                    bestFullRoute = fullRoute;
                }
            }

            if (bestFullRoute == null)
                return ("Не удалось построить маршрут", double.PositiveInfinity);

            return (string.Join(" -> ", bestFullRoute), minDistance);
        }

        private static IEnumerable<IEnumerable<T>> GetPermutations<T>(IEnumerable<T> list, int length)
        {
            if (length == 1)
                return list.Select(t => new T[] { t });

            return GetPermutations(list, length - 1)
                .SelectMany(t => list.Where(e => !t.Contains(e)),
                    (t1, t2) => t1.Concat(new T[] { t2 }));
        }
    }
}
