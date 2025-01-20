# CsvSerializer

.NET用の軽量で使いやすいCSVシリアライザー/デシリアライザーライブラリです。

## 特徴

- オブジェクトからCSV形式への変換（シリアライズ）
- CSV形式からオブジェクトへの変換（デシリアライズ）
- カスタムヘッダー名のサポート（`CsvColumn`属性）
- カスタマイズ可能なオプション
  - 区切り文字の指定
  - ヘッダーの有無
  - 日時フォーマットの指定

## 使用方法

### 基本的な使用例

```csharp
public class Person
{
    public string Name { get; set; }
    public int Age { get; set; }
    public DateTime BirthDate { get; set; }
}

// シリアライズ
var people = new List<Person>
{
    new() { Name = "田中太郎", Age = 30, BirthDate = new DateTime(1993, 5, 15) },
    new() { Name = "山田花子", Age = 25, BirthDate = new DateTime(1998, 8, 20) }
};

string csv = CsvSerializer.Serialize(people);

// デシリアライズ
IEnumerable<Person> deserializedPeople = CsvSerializer.Deserialize<Person>(csv);
```

### カスタムヘッダーの使用

```csharp
public class Person
{
    [CsvColumn("名前")]
    public string Name { get; set; }

    [CsvColumn("年齢")]
    public int Age { get; set; }

    [CsvColumn("生年月日")]
    public DateTime BirthDate { get; set; }
}
```

### カスタムオプションの設定

```csharp
var options = new CsvSerializerOptions
{
    Delimiter = ";",                    // 区切り文字の変更
    IncludeHeader = true,              // ヘッダー行の有無
    DateTimeFormat = "yyyy/MM/dd"      // 日付フォーマットの指定
};

string csv = CsvSerializer.Serialize(people, options);
```

## 要件

- .NET 8.0以上

## ライセンス

このプロジェクトはMITライセンスの下で公開されています。