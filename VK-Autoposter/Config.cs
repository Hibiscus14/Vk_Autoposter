using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Diagnostics;
using VkNet;

namespace VK_Autoposter
{

    internal static class Config
    {
        internal static long GroupId { get; private set; }
        internal static string ImageFolderPath { get; private set; }
        internal static int DaysCount { get; private set; }
        internal static int PostsPerDay { get; private set; }
        internal static List<DayOfWeek> DaysOfWeek { get; private set; }
        internal static List<TimeSpan> PostTimes { get; private set; }
        internal static bool Shuffle { get; private set; }
        internal static bool UsedFolder { get; private set; }
        internal static string AccessToken { get; private set; }

        private static string _configName;
        private static string _configsDir = Directory.GetCurrentDirectory() + "//configs";

        public static void Create()
        {
            Directory.CreateDirectory(Directory.GetCurrentDirectory() + "//configs");
            Console.WriteLine("\n\nВведите название файла конфигурации: ");
            var configName = Console.ReadLine();
            Console.WriteLine("Введите ID группы: ");
            var groupId = Console.ReadLine();
            Console.WriteLine("Введите путь к папке с изображениями: ");
            var imageFolderPath = Console.ReadLine();
            Console.WriteLine("Введите количество дней (0 - без ограничений): ");
            var daysCount = Console.ReadLine();  
            Console.WriteLine("Введите дни недели для публикации через запятую (1-7): ");
            var daysOfWeek = Console.ReadLine();
            Console.WriteLine("Введите количество постов в день (макс. 50): ");
            var postsPerDay = Console.ReadLine();
            Console.WriteLine("Введите время для публикации через запятую, в формате часы:минуты: ");
            var postTimes = Console.ReadLine();
            Console.WriteLine("Перемешивать картинки перед публикацией? (y/n) ");
            var shuffle = Console.ReadKey().Key == ConsoleKey.Y ? "1" : "0";
            Console.WriteLine("\nПеремещать ли успешно опубликованные изображения в отдельную подпапку? (y/n) ");
            var usedFolder = Console.ReadKey().Key == ConsoleKey.Y ? "1" : "0";
            WriteAccessToken();
            var data = new Dictionary<string, string>
            {
                { "GroupId", groupId },
                { "ImageFolderPath", imageFolderPath },
                { "DaysCount", daysCount },
                { "DaysOfWeek", daysOfWeek },
                { "PostsPerDay", postsPerDay },
                { "PostTimes", postTimes},
                { "AccessToken", AccessToken},
                { "Shuffle", shuffle},
                { "UsedFolder", usedFolder},
            };
            _configName = $"{configName}.json";

            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText($"{_configsDir}//{_configName}", json);
            Console.WriteLine($"Файл \"{_configName}\" успешно создан");

        }

        public static void Delete()
        {
            File.Delete(_configName);
            var name = Path.GetFileName(_configName);

            Console.WriteLine($"\nФайл {name} успешно удален.");

        }

        public static void Choose()
        {
            Console.WriteLine("\n\nВыберите файл конфигурации:");
            var configs = Directory.GetFiles(_configsDir);
            if (configs.Length == 0)
            {
                Console.WriteLine("Директория не содержит файлов конфигурации");
                return;
            }
            for (int i = 0; i < configs.Length; i++)
            {
                var name = Path.GetFileName(configs[i]);
                Console.WriteLine($"{i + 1}.{name}");
            }

            Console.WriteLine();
            try
            {
                var configNumber = int.Parse(Console.ReadLine()) - 1;
                _configName = configs[configNumber];
            }
            catch
            {
                Console.WriteLine("Некорректное значение номера");
                Environment.Exit(0);
            }

        }

        public static void Read()
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(_configsDir)
                .AddJsonFile(_configName, optional: false, reloadOnChange: true)
                .Build();

            GroupId = long.Parse(config["GroupId"]);
            ImageFolderPath = config["ImageFolderPath"];
            DaysCount = int.Parse(config["DaysCount"]);
            PostsPerDay = int.Parse(config["PostsPerDay"]);

            var daysInput = config["DaysOfWeek"];
            var daysOfWeek = daysInput.Split(',').Select(int.Parse).ToList();

            DaysOfWeek = new List<DayOfWeek>(daysOfWeek.Count);
            for (int i = 0; i < daysOfWeek.Count; i++)
            {
                if (daysOfWeek[i] == 7) daysOfWeek[i] = 0;
                DaysOfWeek.Add((DayOfWeek)daysOfWeek[i]);

            }

            string postTimeInput = config["PostTimes"];
            PostTimes = postTimeInput.Split(',').Select(TimeSpan.Parse).ToList();
            Shuffle = config["Shuffle"] == "1";
            UsedFolder = config["UsedFolder"] == "1";

            AccessToken = config["AccessToken"];
        }

        public static void WriteAccessToken()
        {
            var vk = new VkApi();
            var version = vk.VkApiVersion;
            string url = ("https://oauth.vk.com/authorize?client_id=" + 52358627 + "&display=page&redirect_uri=https://oauth.vk.com/blank.html&scope=offline,wall,photos,groups&response_type=token&v=" + version);

            Process myProcess = new Process();
            myProcess.StartInfo.UseShellExecute = true;
            myProcess.StartInfo.FileName = url;
            myProcess.Start();

            Console.Write("\nНажмите \"продолжить\" и введите полученный AccessToken: ");
            var accessToken = Console.ReadLine();
            if (accessToken != null)
            {
                if (accessToken.Contains("oauth"))
                    accessToken = ParseUrl(accessToken);

                AccessToken = accessToken;
                if (File.Exists($"{_configsDir}//{_configName}"))
                {
                    string json = File.ReadAllText($"{_configsDir}//{_configName}");
                    dynamic jsonObj = Newtonsoft.Json.JsonConvert.DeserializeObject(json);

                    SetValueRecursively("AccessToken", jsonObj, accessToken);

                    string output = Newtonsoft.Json.JsonConvert.SerializeObject(jsonObj, Newtonsoft.Json.Formatting.Indented);
                    File.WriteAllText($"{_configsDir}//{_configName}", output);
                }
            }
        }

        private static string ParseUrl(string url)
        {
            string temp = url.Split('#')[1];

            return temp.Split('&')[0].Split('=')[1];

        }
        private static void SetValueRecursively<T>(string sectionPathKey, dynamic jsonObj, T value)
        {
            var remainingSections = sectionPathKey.Split(":", 2);

            var currentSection = remainingSections[0];
            if (remainingSections.Length > 1)
            {
                var nextSection = remainingSections[1];
                SetValueRecursively(nextSection, jsonObj[currentSection], value);
            }
            else
            {
                jsonObj[currentSection] = value;
            }
        }
    }
}
