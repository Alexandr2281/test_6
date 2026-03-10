using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RouteFinder
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Программа поиска кратчайшего маршрута через три точки");
            Console.WriteLine("г. Кольчугино\n");

            string fileName;
            double[,] graph;
            int nodeCount;

            // Загрузка данных из файла
            while (true)
            {
                Console.Write("Введите имя файла с данными о карте: ");
                fileName = Console.ReadLine();

                try
                {
                    (graph, nodeCount) = LoadFromFile(fileName);
                    Console.WriteLine($"Данные успешно загружены. Количество точек на карте: {nodeCount}\n");
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка загрузки файла: {ex.Message}");
                }
            }

            // Вычисление матрицы кратчайших расстояний и матрицы маршрутов
            var (shortestPaths, next) = FloydWithPath(graph, nodeCount);

            // Основной цикл работы с пользователем
            while (true)
            {
                Console.WriteLine("\nВведите номера трёх точек (для выхода введите 0 в любом поле):");

                int[] points = new int[3];
                bool exitRequested = false;

                for (int i = 0; i < 3; i++)
                {
                    Console.Write($"Точка {i + 1}: ");
                    if (!int.TryParse(Console.ReadLine(), out points[i]))
                    {
                        Console.WriteLine("Ошибка: введите целое число");
                        i--;
                        continue;
                    }

                    if (points[i] == 0)
                    {
                        exitRequested = true;
                        break;
                    }

                    if (points[i] < 1 || points[i] > nodeCount)
                    {
                        Console.WriteLine($"Ошибка: номер точки должен быть от 1 до {nodeCount}");
                        i--;
                    }
                }

                if (exitRequested)
                {
                    Console.WriteLine("Программа завершена.");
                    break;
                }

                // Поиск оптимального маршрута
                var result = FindBestRouteWithPath(points, shortestPaths, next);

                // Вывод результатов
                if (double.IsInfinity(result.Distance))
                {
                    Console.WriteLine($"\n{result.Route}");
                }
                else
                {
                    Console.WriteLine($"\nОптимальный маршрут: {result.Route}");
                    Console.WriteLine($"Длина маршрута: {result.Distance:F2} км");
                }
            }
        }

        // Загрузка данных из файла (только числа, без комментариев)
        static (double[,] graph, int nodeCount) LoadFromFile(string fileName)
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
                    // Пробуем разные форматы чисел
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

            // Создаем матрицу смежности
            double[,] graph = new double[maxNode, maxNode];
            for (int i = 0; i < maxNode; i++)
                for (int j = 0; j < maxNode; j++)
                    graph[i, j] = (i == j) ? 0 : double.PositiveInfinity;

            // Заполняем рёбра
            foreach (var edge in edges)
            {
                // Индексы в матрице с 0
                graph[edge.from - 1, edge.to - 1] = edge.distance;
                graph[edge.to - 1, edge.from - 1] = edge.distance; // Неориентированный граф
            }

            return (graph, maxNode);
        }

        // Реализация алгоритма Флойда с сохранением маршрутов
        static (double[,] dist, int[,] next) FloydWithPath(double[,] a, int n)
        {
            double[,] dist = (double[,])a.Clone();
            int[,] next = new int[n, n];

            // Инициализация матрицы next
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    if (!double.IsInfinity(dist[i, j]) && i != j)
                        next[i, j] = j;
                    else
                        next[i, j] = -1;

            // Алгоритм Флойда
            for (int k = 0; k < n; k++)
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < n; j++)
                        if (dist[i, k] + dist[k, j] < dist[i, j])
                        {
                            dist[i, j] = dist[i, k] + dist[k, j];
                            next[i, j] = next[i, k];
                        }

            return (dist, next);
        }

        // Восстановление пути между двумя точками
        static List<int> GetPath(int from, int to, int[,] next)
        {
            var path = new List<int>();

            if (next[from, to] == -1)
                return path;

            path.Add(from + 1); // Переводим в 1-индексацию для вывода

            int current = from;
            while (current != to)
            {
                current = next[current, to];
                path.Add(current + 1);
            }

            return path;
        }

        // Поиск оптимального маршрута через три точки с полным путём
        static (string Route, double Distance) FindBestRouteWithPath(int[] points, double[,] distances, int[,] next)
        {
            // Проверка связности точек
            foreach (int p1 in points)
                foreach (int p2 in points)
                    if (p1 != p2 && double.IsInfinity(distances[p1 - 1, p2 - 1]))
                        return ($"Нет пути между точками {p1} и {p2}", double.PositiveInfinity);

            // Все возможные перестановки трёх точек (3! = 6 вариантов)
            var permutations = GetPermutations(points, 3);

            double minDistance = double.PositiveInfinity;
            List<int> bestFullRoute = null;

            foreach (var perm in permutations)
            {
                int[] route = perm.ToArray();

                // Получаем полные пути между точками
                var path1 = GetPath(route[0] - 1, route[1] - 1, next);
                var path2 = GetPath(route[1] - 1, route[2] - 1, next);

                // Объединяем пути (избегаем дублирования средней точки)
                var fullRoute = new List<int>();
                fullRoute.AddRange(path1);

                // Добавляем путь 2, начиная со второй точки (чтобы не дублировать точку стыка)
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

            return (string.Join(" -> ", bestFullRoute), minDistance);
        }

        // Генерация всех перестановок
        static IEnumerable<IEnumerable<T>> GetPermutations<T>(IEnumerable<T> list, int length)
        {
            if (length == 1) return list.Select(t => new T[] { t });
            return GetPermutations(list, length - 1)
                .SelectMany(t => list.Where(e => !t.Contains(e)),
                    (t1, t2) => t1.Concat(new T[] { t2 }));
        }
    }
}