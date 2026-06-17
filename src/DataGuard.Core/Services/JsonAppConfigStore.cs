using System.Text.Json;
using System.Text.Json.Serialization;
using DataGuard.Core.Abstractions;
using DataGuard.Core.Models;

namespace DataGuard.Core.Services;

/// <summary>
/// 앱 설정을 LocalAppData의 JSON 파일에 저장한다. 단일 사용자 데스크탑 앱이라 파일이면 충분.
/// (System.Text.Json은 BCL 내장 — 외부 의존성 없음.)
/// </summary>
public sealed class JsonAppConfigStore : IAppConfigStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        // enum을 숫자가 아닌 이름으로 저장해 설정 파일 가독성을 높인다.
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _path;

    public JsonAppConfigStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DataGuard",
            "config.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
    }

    public AppConfig Load()
    {
        if (!File.Exists(_path))
        {
            return new AppConfig();
        }

        try
        {
            string json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<AppConfig>(json, Options) ?? new AppConfig();
        }
        catch (JsonException)
        {
            // 설정 파일이 손상된 경우 앱이 죽지 않도록 빈 설정으로 시작한다.
            // (운영 시에는 손상 파일을 백업해두는 처리를 추가하는 것이 좋다.)
            return new AppConfig();
        }
    }

    public void Save(AppConfig config)
    {
        string json = JsonSerializer.Serialize(config, Options);

        // 쓰기 도중 중단되어 파일이 깨지는 것을 막기 위해 임시 파일에 쓴 뒤 교체한다.
        string tempPath = _path + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _path, overwrite: true);
    }
}
