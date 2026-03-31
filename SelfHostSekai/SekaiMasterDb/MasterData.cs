using System.Collections.Frozen;
using System.Text.Json;

namespace SekaiMasterDb;

public class MasterData<T>
{
    private readonly Lazy<IReadOnlyList<T>> _data;
    private readonly Lazy<FrozenDictionary<int, T>> _indexedData;

    public MasterData(string filePath)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, IncludeFields = true };
        _data = new Lazy<IReadOnlyList<T>>(() =>
        {
            if (!File.Exists(filePath))
                return [];
                
            using var stream = File.OpenRead(filePath);
            return JsonSerializer.Deserialize<T[]>(stream, options) ?? [];
        });

        _indexedData = new Lazy<FrozenDictionary<int, T>>(() =>
        {
            var dict = new Dictionary<int, T>();
            var type = typeof(T);
            var idField = type.GetField("id");
            var idProp = type.GetProperty("id");
            
            foreach (var item in _data.Value)
            {
                if (idField != null && idField.FieldType == typeof(int))
                {
                    dict[(int)idField.GetValue(item)!] = item;
                }
                else if (idProp != null && idProp.PropertyType == typeof(int))
                {
                    dict[(int)idProp.GetValue(item)!] = item;
                }
            }
            return dict.ToFrozenDictionary();
        });
    }

    public IReadOnlyList<T> All => _data.Value;
    
    public T? GetById(int id)
    {
        return _indexedData.Value.GetValueOrDefault(id);
    }
}
