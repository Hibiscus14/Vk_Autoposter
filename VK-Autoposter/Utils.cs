namespace VK_Autoposter
{
    internal static class Utils
    {

        public static Dictionary<string, DateTime> GeneratePublishSchedule(List<string> images, List<DayOfWeek> daysOfWeek, int imagesPerDay, List<TimeSpan> publishTimes)
        {
            Dictionary<string, DateTime> schedule = new Dictionary<string, DateTime>();
            DateTime startDate = DateTime.Now.Date;

            var imagesCount = Config.DaysCount;
            if (imagesCount == 0 || imagesCount > images.Count) imagesCount = images.Count;
            if (imagesCount > 300) imagesCount = 300;

            int publishTimeIndex = 0; 
            for (int i = 0; i < imagesCount; i++)
            {
                DateTime publishDate = GetNextPublishDate(ref startDate, daysOfWeek, publishTimes[publishTimeIndex]);

                if (!schedule.ContainsValue(publishDate))
                {
                    schedule.Add(images[i], publishDate);

                    publishTimeIndex = (publishTimeIndex + 1) % publishTimes.Count;
                }

                if ((i + 1) % imagesPerDay == 0)
                    startDate = startDate.AddDays(1);
            }

            return schedule;
        }

        private static DateTime GetNextPublishDate(ref DateTime startDate, List<DayOfWeek> daysOfWeek, TimeSpan publishTime)
        {
            while (!daysOfWeek.Contains(startDate.DayOfWeek))
            {
                startDate = startDate.AddDays(1);
            }

            return startDate.Date.Add(publishTime);
        }

        public static async Task LogErrorAsync(string image, DateTime date, Exception ex)
        {
            string logFilePath = "error_log.txt";
            using (StreamWriter writer = new StreamWriter(logFilePath, true))
            {
                await writer.WriteLineAsync($"{DateTime.Now}: Ошибка при публикации поста {image} на дату {date}: {ex.Message}\n");
                await writer.WriteLineAsync(ex.StackTrace);
            }
        }

        public static async Task MoveImageAsync(string imagePath, string time)
        {
            string resultsDirectory = Path.Combine(Config.ImageFolderPath, "results");
            if (!Directory.Exists(resultsDirectory))
                Directory.CreateDirectory(resultsDirectory);

            string timeDirectory = Path.Combine(resultsDirectory, time);
            if (!Directory.Exists(timeDirectory))
                Directory.CreateDirectory(timeDirectory);

            string destinationPath = Path.Combine(timeDirectory, Path.GetFileName(imagePath));

            await Task.Run(() => File.Move(imagePath, destinationPath));
        }

    }
}