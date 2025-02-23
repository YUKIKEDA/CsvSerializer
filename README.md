# CsvSerializer

.NET用の軽量で使いやすいCSVシリアライザー/デシリアライザーライブラリです。

## 特徴

- オブジェクトからCSV形式への変換（シリアライズ）
- CSV形式からオブジェクトへの変換（デシリアライズ）
- カスタムヘッダー名のサポート（`CsvColumn`属性）
- 辞書型（Dictionary）のサポート
- カスタマイズ可能なオプション
  - 区切り文字の指定
  - ヘッダーの有無
  - 日時フォーマットの指定
  - カルチャー情報の指定
- 堅牢なエラーハンドリング
- 高いパフォーマンス（StringBuilder最適化）
- Nullableタイプのサポート

## インストール

NuGetパッケージマネージャーを使用してインストールできます：

```bash
dotnet add package CsvSerializer
```

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
    DateTimeFormat = "yyyy/MM/dd",     // 日付フォーマットの指定
    Culture = new CultureInfo("ja-JP") // カルチャー情報の指定
};

string csv = CsvSerializer.Serialize(people, options);
```

### 辞書型の使用

```csharp
public class ScoreRecord
{
    public string Name { get; set; }
    public Dictionary<string, int> Scores { get; set; }
}

var records = new List<ScoreRecord>
{
    new() {
        Name = "田中太郎",
        Scores = new Dictionary<string, int> {
            { "数学", 85 },
            { "国語", 90 }
        }
    }
};

// シリアライズ結果：
// Name,数学,国語
// 田中太郎,85,90
```

### Nullableタイプの使用

```csharp
public class Employee
{
    public string? Name { get; set; }
    public int? Age { get; set; }        // Nullable int
    public DateTime? StartDate { get; set; }  // Nullable DateTime
    public UserStatus? Status { get; set; }   // Nullable Enum
}
```

## サポートされているデータ型

- プリミティブ型（`int`, `long`, `float`, `double`, `bool`など）
- `string`
- `DateTime`
- 列挙型（`enum`）
- 辞書型（`Dictionary<TKey, TValue>`）※キーと値は上記のサポート型に限る
- 上記の型のNullableバージョン

## エラーハンドリング

ライブラリは以下の場合に`CsvSerializationException`を投げます：

- 無効なCSVフォーマット
- データ型の変換エラー
- 必須ヘッダーの欠落
- サポートされていない型の使用
- 空のCSVデータ
- 無効な列挙型の値

```csharp
try
{
    var result = CsvSerializer.Deserialize<Person>(invalidCsvData);
}
catch (CsvSerializationException ex)
{
    Console.WriteLine($"CSVの処理中にエラーが発生しました: {ex.Message}");
}
```

## パフォーマンスの考慮事項

- 大量のデータを処理する場合は、`Deserialize`メソッドが返す`IEnumerable<T>`を利用して、メモリ効率の良い処理が可能です
- シリアライズ時は`StringBuilder`を使用して最適化されています
- 空の行は自動的にスキップされます

## 要件

- .NET 8.0以上

## ライセンス

このプロジェクトはMITライセンスの下で公開されています。詳細は[LICENSE](LICENSE)ファイルをご覧ください。

## コントリビューション

バグ報告や機能リクエストは、GitHubのIssueトラッカーをご利用ください。
プルリクエストも歓迎します。
