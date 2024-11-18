using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using VkNet;
using VkNet.Model;

namespace VK_Autoposter
{
    internal static class Autoposter
    {
        private static VkApi vk;
        private static readonly SemaphoreSlim apiRateLimitSemaphore = new SemaphoreSlim(1, 1);
        private static readonly TimeSpan apiDelay = TimeSpan.FromMilliseconds(333);
        private static readonly HttpClient httpClient = new HttpClient();

        static async Task Main(string[] args)
        {
            vk = new VkApi();

            while (true)
            {
                Console.WriteLine("Выберите пункт меню: \n1 - Создать новый файл конфигурации \n2 - Выбрать файл конфигурации из уже существующих \n3 - Удалить файл конфигурации \n4 - Выйти\n ");
                var choice = Console.ReadKey();
                switch (choice.Key)
                {
                    case ConsoleKey.D1:
                        Config.Create();
                        Console.WriteLine("\nНачать публиацию? (y/n)");
                        var answer = Console.ReadKey().Key;
                        if (answer == ConsoleKey.Y)
                            await ReadDataAsync();
                        break;
                    case ConsoleKey.D2:
                        Config.Choose();
                        await ReadDataAsync();
                        break;
                    case ConsoleKey.D3:
                        Config.Choose();
                        Config.Delete();
                        break;
                    case ConsoleKey.D4:
                        Environment.Exit(1);
                        break;

                    default:
                        Console.WriteLine("\nНекорректное значение");
                        return;
                }
            }
            Console.ReadKey();
        }

        private static async Task ReadDataAsync()
        {
            try
            {
                Config.Read();
                if (Config.AccessToken == null)
                {
                    Config.WriteAccessToken();
                }
                vk.Authorize(new ApiAuthParams { AccessToken = Config.AccessToken });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nОшибка входных данных: {ex}");
            }

            await StartPostingAsync();
        }

        private static async Task StartPostingAsync()
        {
            var attemptTime = DateTime.Now.ToShortDateString();
            var images = Directory.GetFiles(Config.ImageFolderPath, "*.jpg")
                                  .Concat(Directory.GetFiles(Config.ImageFolderPath, "*.png")).ToList();

            if (images.Count == 0)
            {
                Console.WriteLine("\nВ папке нет изображений.");
                return;
            }

            if (Config.Shuffle) Random.Shared.Shuffle(CollectionsMarshal.AsSpan(images));

            var publishSchedule = Utils.GeneratePublishSchedule(images, Config.DaysOfWeek, Config.PostsPerDay, Config.PostTimes);
            Console.WriteLine();

            var postsToPublish = new ConcurrentDictionary<string, DateTime>();

            await PublishPostsAsync(publishSchedule, attemptTime, postsToPublish);

            while (postsToPublish.Count > 0)
            {
                Console.WriteLine($"\nКоличество постов с ошибками: {postsToPublish.Count}. Попробовать снова? (y/n)");

                var answer = Console.ReadKey().Key;
                if (answer == ConsoleKey.Y)
                {
                    Console.WriteLine("\nПовторная попытка публикации...");
                    var retryPosts = new Dictionary<string, DateTime>(postsToPublish);
                    postsToPublish.Clear();
                    await PublishPostsAsync(retryPosts, attemptTime, postsToPublish);
                }
                else
                {
                    Console.WriteLine("\nЗавершение работы программы.");
                    return;
                }
            }

            Console.WriteLine($"\nВсе посты успешно опубликованы!");
        }

        private static async Task PublishPostsAsync(Dictionary<string, DateTime> postsToPublish, string attemptTime, ConcurrentDictionary<string, DateTime> unsuccessfullyPublished)
        {
            var tasks = postsToPublish.Select(async post =>
            {
                try
                {
                    await PostImageToWallAsync(post);
                    if (Config.UsedFolder) await Utils.MoveImageAsync(post.Key, attemptTime);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\nОшибка при публикации поста {post.Key} на дату {post.Value}: {ex}");
                    await Utils.LogErrorAsync(post.Key, post.Value, ex);
                    unsuccessfullyPublished.TryAdd(post.Key, post.Value);
                }
            });

            await Task.WhenAll(tasks);
        }

        private static async Task PostImageToWallAsync(KeyValuePair<string, DateTime> post)
        {
            await apiRateLimitSemaphore.WaitAsync();
            try
            {
                var uploadServer = await vk.Photo.GetWallUploadServerAsync(Config.GroupId);
                await Task.Delay(apiDelay);

                using var content = new MultipartFormDataContent
                {
                    { new ByteArrayContent(await File.ReadAllBytesAsync(post.Key)), "file", Path.GetFileName(post.Key) }
                };

                var uploadResponse = await httpClient.PostAsync(uploadServer.UploadUrl, content);
                uploadResponse.EnsureSuccessStatusCode();

                var responseFile = await uploadResponse.Content.ReadAsStringAsync();
                await Task.Delay(apiDelay);

                var photos = await vk.Photo.SaveWallPhotoAsync(responseFile, null, (ulong)Config.GroupId);
                await Task.Delay(apiDelay);

                await vk.Wall.PostAsync(new WallPostParams
                {
                    OwnerId = -Config.GroupId,
                    Attachments = photos,
                    FromGroup = true,
                    Signed = false,
                    PublishDate = post.Value
                });

                Console.WriteLine($"Запланирован пост: {post.Key} на {post.Value}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при публикации поста {post.Key}: {ex.Message}");
                throw;
            }
            finally
            {
                apiRateLimitSemaphore.Release();
            }
        }
    }
}
