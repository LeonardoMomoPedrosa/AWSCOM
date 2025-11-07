namespace Personalize.Services;

public class ExecutionStateService
{
    private readonly string _stateFilePath;

    public ExecutionStateService(string stateFilePath)
    {
        _stateFilePath = stateFilePath;
    }

    public async Task<DateTime?> GetLastProcessedDateAsync()
    {
        if (!File.Exists(_stateFilePath))
        {
            return null;
        }

        try
        {
            var content = await File.ReadAllTextAsync(_stateFilePath);
            if (DateTime.TryParse(content.Trim(), out var date))
            {
                return date;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ⚠️  Erro ao ler arquivo de estado: {ex.Message}");
        }

        return null;
    }

    public async Task SaveLastProcessedDateAsync(DateTime date)
    {
        try
        {
            var directory = Path.GetDirectoryName(_stateFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(_stateFilePath, date.ToString("yyyy-MM-dd HH:mm:ss"));
            Console.WriteLine($"   ✅ Data de última execução salva: {date:yyyy-MM-dd HH:mm:ss}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ⚠️  Erro ao salvar arquivo de estado: {ex.Message}");
        }
    }
}

