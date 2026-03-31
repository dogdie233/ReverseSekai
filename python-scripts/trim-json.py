import json
import os

def trim_json_arrays(data, limit=5):
    """
    递归遍历 JSON 数据，如果发现列表长度超过 limit，则进行截断。
    """
    if isinstance(data, list):
        # 如果是列表，先截断前 limit 个元素
        trimmed_list = data[:limit]
        # 然后对剩下的每个元素继续递归（防止元素本身是字典或列表）
        return [trim_json_arrays(item, limit) for item in trimmed_list]
    
    elif isinstance(data, dict):
        # 如果是字典，递归处理所有的 value
        return {k: trim_json_arrays(v, limit) for k, v in data.items()}
    
    else:
        # 如果是基本类型（字符串、数字、布尔等），直接返回
        return data

def process_file(input_file, output_file, limit=5):
    try:
        # 1. 读取 JSON 文件
        if not os.path.exists(input_file):
            print(f"错误: 找不到文件 {input_file}")
            return

        with open(input_file, 'r', encoding='utf-8') as f:
            raw_data = json.load(f)

        # 2. 处理数据
        processed_data = trim_json_arrays(raw_data, limit)

        # 3. 写入新文件
        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(processed_data, f, indent=4, ensure_ascii=False)
        
        print(f"处理完成！已将结果保存至: {output_file}")

    except json.JSONDecodeError:
        print("错误: 文件不是有效的 JSON 格式")
    except Exception as e:
        print(f"发生未知错误: {e}")

if __name__ == "__main__":
    # 配置区
    INPUT_PATH = 'input.json'   # 你的原始文件名
    OUTPUT_PATH = 'output.json' # 处理后的文件名
    MAX_ITEMS = 3               # 数组保留的最大数量

    process_file(INPUT_PATH, OUTPUT_PATH, MAX_ITEMS)