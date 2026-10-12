using System.Text.Json;

namespace AiSeasonCreator.Repos
{
    public class JsonRepo : IJsonRepo
    {
        public T Load<T>(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            return JsonSerializer.Deserialize<T>(stream, LenientJson.Options)!;
        }

        public void Save<T>(string filePath, T data)
        {
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
            };

            string jsonString = JsonSerializer.Serialize(data, jsonOptions);

            // Write to a temp file first so a crash never leaves a half-written season/roster behind.
            var tempPath = filePath + ".tmp";
            File.WriteAllText(tempPath, jsonString);
            File.Move(tempPath, filePath, overwrite: true);
        }
    }
}
