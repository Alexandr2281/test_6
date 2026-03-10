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
            // Создаем тестовый файл с данными
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
            // Arrange
            Assert.IsNotNull(_testFilePath, "Тестовый файл не создан");

            int[] points = new int[] { 1, 3, 4 };

            // Загружаем граф из тестового файла
            var (graph, nodeCount) = RouteFinderHelper.LoadFromFile(_testFilePath);

            // Вычисляем матрицу кратчайших расстояний и маршрутов
            var (distances, next) = RouteFinderHelper.FloydWithPath(graph, nodeCount);

            // Act
            var result = RouteFinderHelper.FindBestRouteWithPath(points, distances, next);

            // Assert
            Assert.IsFalse(double.IsInfinity(result.Distance), "Маршрут должен существовать");
            Assert.AreEqual(7.3, Math.Round(result.Distance, 1), "Длина маршрута должна быть 7.3 км");

            // Проверяем, что маршрут содержит все три точки
            Assert.IsNotNull(result.Route, "Маршрут не должен быть null");

            var routePoints = result.Route.Split(new[] { " -> " }, StringSplitOptions.None)
                                          .Select(int.Parse)
                                          .ToList();

            Assert.IsTrue(routePoints.Contains(1), "Маршрут должен содержать точку 1");
            Assert.IsTrue(routePoints.Contains(3), "Маршрут должен содержать точку 3");
            Assert.IsTrue(routePoints.Contains(4), "Маршрут должен содержать точку 4");

            // Проверяем, что маршрут корректен (возможны два варианта)
            bool validRoute1 = result.Route == "3 -> 2 -> 1 -> 4";
            bool validRoute2 = result.Route == "4 -> 1 -> 2 -> 3";

            Assert.IsTrue(validRoute1 || validRoute2,
                $"Маршрут должен быть '3 -> 2 -> 1 -> 4' или '4 -> 1 -> 2 -> 3', получен: {result.Route}");
        }

        [TestMethod]
        public void FloydAlgorithm_CorrectlyComputesShortestPaths()
        {
            // Arrange
            Assert.IsNotNull(_testFilePath, "Тестовый файл не создан");

            var (graph, nodeCount) = RouteFinderHelper.LoadFromFile(_testFilePath);

            // Act
            var (distances, next) = RouteFinderHelper.FloydWithPath(graph, nodeCount);

            // Assert - проверяем известные кратчайшие пути
            Assert.AreEqual(2.5, distances[0, 1], 0.001, "Расстояние 1-2 должно быть 2.5"); // 1-2 прямое ребро
            Assert.AreEqual(4.3, distances[0, 2], 0.001, "Расстояние 1-3 должно быть 4.3"); // 1-3 через 2: 2.5+1.8=4.3
            Assert.AreEqual(3.0, distances[0, 3], 0.001, "Расстояние 1-4 должно быть 3.0"); // 1-4 прямое ребро
            Assert.AreEqual(4.7, distances[0, 4], 0.001, "Расстояние 1-5 должно быть 4.7"); // 1-5 через 2: 2.5+2.2=4.7

            // Путь 1-2-3-6: 2.5 + 1.8 + 1.5 = 5.8
            Assert.AreEqual(5.8, distances[0, 5], 0.001, "Расстояние 1-6 должно быть 5.8");
        }

        [TestMethod]
        public void GetPath_ReconstructsCorrectRoute()
        {
            // Arrange
            Assert.IsNotNull(_testFilePath, "Тестовый файл не создан");

            var (graph, nodeCount) = RouteFinderHelper.LoadFromFile(_testFilePath);
            var (distances, next) = RouteFinderHelper.FloydWithPath(graph, nodeCount);

            // Act - восстанавливаем путь от 1 до 3
            var path = RouteFinderHelper.GetPath(0, 2, next); // 0-index: 1->3

            // Assert - путь должен быть 1 -> 2 -> 3
            Assert.IsNotNull(path, "Путь не должен быть null");
            Assert.AreEqual(3, path.Count, "Путь должен содержать 3 точки");
            Assert.AreEqual(1, path[0], "Первая точка должна быть 1");
            Assert.AreEqual(2, path[1], "Вторая точка должна быть 2");
            Assert.AreEqual(3, path[2], "Третья точка должна быть 3");

            // Act - путь от 3 до 4
            path = RouteFinderHelper.GetPath(2, 3, next); // 3->4

            // Должен быть 3 -> 2 -> 5 -> 4 или 3 -> 6 -> 5 -> 4
            Assert.IsNotNull(path, "Путь не должен быть null");
            Assert.IsTrue(path.Count >= 4, "Путь должен содержать минимум 4 точки");
            Assert.AreEqual(3, path[0], "Первая точка должна быть 3");
            Assert.AreEqual(4, path[path.Count - 1], "Последняя точка должна быть 4");
        }

        [TestMethod]
        public void LoadFromFile_ValidFile_ReturnsCorrectGraph()
        {
            // Arrange
            Assert.IsNotNull(_testFilePath, "Тестовый файл не создан");

            // Act
            var (graph, nodeCount) = RouteFinderHelper.LoadFromFile(_testFilePath);

            // Assert
            Assert.AreEqual(6, nodeCount, "Граф должен содержать 6 вершин");
            Assert.AreEqual(2.5, graph[0, 1], 0.001, "Ребро 1-2 должно быть 2.5");
            Assert.AreEqual(1.8, graph[1, 2], 0.001, "Ребро 2-3 должно быть 1.8");
            Assert.AreEqual(3.0, graph[0, 3], 0.001, "Ребро 1-4 должно быть 3.0");
            Assert.AreEqual(double.PositiveInfinity, graph[0, 5], "Прямого ребра 1-6 быть не должно");
        }
    }

    /// <summary>
    /// Статический вспомогательный класс для тестирования
    /// </summary>
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
                        if (dist[i, k] + dist[k, j] < dist[i, j] - 0.0001) // допуск на погрешность
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
            // Проверка на null
            if (points == null || points.Length != 3)
                return ("Некорректные точки", double.PositiveInfinity);

            // Проверка связности
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