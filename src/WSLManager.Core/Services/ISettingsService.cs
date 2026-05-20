namespace WSLManager.Core.Services;

using WSLManager.Core.Models;

public interface ISettingsService
{
    AppSettings Load();
    void Save(AppSettings settings);
}
