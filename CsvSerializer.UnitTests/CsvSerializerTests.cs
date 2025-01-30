namespace CsvSerializer.UnitTests
{
    // 基本的なテストデータ用のクラス
    public class TestClass
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
        public DateTime BirthDate { get; set; }
    }

    public class TestClassWithCustomHeaders
    {
        [CsvColumn("名前")]
        public string Name { get; set; } = "";

        [CsvColumn("年齢")]
        public int Age { get; set; }

        [CsvColumn("生年月日")]
        public DateTime BirthDate { get; set; }
    }

    // 基本的なシリアライズ/デシリアライズのテスト
    public class BasicCsvSerializerTests
    {
        [Fact]
        public void Serialize_BasicTest()
        {
            // Arrange
            var data = new List<TestClass>
            {
                new() { Name = "田中太郎", Age = 30, BirthDate = new DateTime(1993, 5, 15) },
                new() { Name = "山田花子", Age = 25, BirthDate = new DateTime(1998, 8, 20) }
            };

            // Act
            var csv = CsvSerializer.Serialize(data);

            // Assert
            var expected = "Name,Age,BirthDate\r\n" +
                        "田中太郎,30,1993-05-15 00:00:00\r\n" +
                        "山田花子,25,1998-08-20 00:00:00\r\n";
            Assert.Equal(expected, csv);
        }

        [Fact]
        public void Deserialize_BasicTest()
        {
            // Arrange
            var csv = "Name,Age,BirthDate\r\n" +
                    "田中太郎,30,1993-05-15 00:00:00\r\n" +
                    "山田花子,25,1998-08-20 00:00:00\r\n";

            // Act
            var result = CsvSerializer.Deserialize<TestClass>(csv).ToList();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal("田中太郎", result[0].Name);
            Assert.Equal(30, result[0].Age);
            Assert.Equal(new DateTime(1993, 5, 15), result[0].BirthDate);
            Assert.Equal("山田花子", result[1].Name);
            Assert.Equal(25, result[1].Age);
            Assert.Equal(new DateTime(1998, 8, 20), result[1].BirthDate);
        }

        [Fact]
        public void Serialize_WithSpecialCharacters()
        {
            // Arrange
            var data = new List<TestClass>
            {
                new() { Name = "田中,太郎", Age = 30, BirthDate = new DateTime(1993, 5, 15) }
            };

            // Act
            var csv = CsvSerializer.Serialize(data);

            // Assert
            var expected = "Name,Age,BirthDate\r\n" +
                        "\"田中,太郎\",30,1993-05-15 00:00:00\r\n";
            Assert.Equal(expected, csv);
        }

        [Fact]
        public void Serialize_EmptyList()
        {
            // Arrange
            var data = new List<TestClass>();

            // Act
            var csv = CsvSerializer.Serialize(data);

            // Assert
            var expected = "Name,Age,BirthDate\r\n";
            Assert.Equal(expected, csv);
        }

        [Fact]
        public void Deserialize_EmptyData()
        {
            // Arrange
            var csv = "Name,Age,BirthDate\r\n";

            // Act
            var result = CsvSerializer.Deserialize<TestClass>(csv).ToList();

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void Deserialize_WithNullValues()
        {
            // Arrange
            var csv = "Name,Age,BirthDate\r\n" +
                    ",,\r\n";

            // Act
            var result = CsvSerializer.Deserialize<TestClass>(csv).ToList();

            // Assert
            Assert.Single(result);
            Assert.Null(result[0].Name);  // 空フィールドはnullとして扱われる
            Assert.Equal(0, result[0].Age);
            Assert.Equal(default, result[0].BirthDate);
        }
    }

    // カスタムオプションを使用したテスト
    public class CustomOptionsCsvSerializerTests
    {
        [Fact]
        public void Serialize_WithCustomOptions()
        {
            // Arrange
            var data = new List<TestClass>
            {
                new() { Name = "田中太郎", Age = 30, BirthDate = new DateTime(1993, 5, 15) }
            };
            var options = new CsvSerializerOptions
            {
                Delimiter = ";",
                DateTimeFormat = "yyyy/MM/dd",
                IncludeHeader = false
            };

            // Act
            var csv = CsvSerializer.Serialize(data, options);

            // Assert
            var expected = "田中太郎;30;1993/05/15\r\n";
            Assert.Equal(expected, csv);
        }

        [Fact]
        public void Serialize_WithCustomCulture()
        {
            // Arrange
            var data = new List<TestClass>
            {
                new() { Name = "田中太郎", Age = 30, BirthDate = new DateTime(1993, 5, 15) }
            };
            var options = new CsvSerializerOptions
            {
                Culture = new System.Globalization.CultureInfo("ja-JP")
            };

            // Act
            var csv = CsvSerializer.Serialize(data, options);

            // Assert
            var expected = "Name,Age,BirthDate\r\n" +
                        "田中太郎,30,1993-05-15 00:00:00\r\n";
            Assert.Equal(expected, csv);
        }

        [Fact]
        public void Deserialize_WithCustomCulture()
        {
            // Arrange
            var csv = "Name,Age,BirthDate\r\n" +
                    "田中太郎,30,1993/05/15\r\n";
            var options = new CsvSerializerOptions
            {
                Culture = new System.Globalization.CultureInfo("ja-JP"),
                DateTimeFormat = "yyyy/MM/dd"
            };

            // Act
            var result = CsvSerializer.Deserialize<TestClass>(csv, options).ToList();

            // Assert
            Assert.Single(result);
            Assert.Equal("田中太郎", result[0].Name);
            Assert.Equal(30, result[0].Age);
            Assert.Equal(new DateTime(1993, 5, 15), result[0].BirthDate);
        }
    }

    // カスタムヘッダーを使用したテスト
    public class CustomHeadersCsvSerializerTests
    {
        [Fact]
        public void Serialize_WithCustomHeaders()
        {
            // Arrange
            var data = new List<TestClassWithCustomHeaders>
            {
                new() { Name = "田中太郎", Age = 30, BirthDate = new DateTime(1993, 5, 15) },
                new() { Name = "山田花子", Age = 25, BirthDate = new DateTime(1998, 8, 20) }
            };

            // Act
            var csv = CsvSerializer.Serialize(data);

            // Assert
            var expected = "名前,年齢,生年月日\r\n" +
                        "田中太郎,30,1993-05-15 00:00:00\r\n" +
                        "山田花子,25,1998-08-20 00:00:00\r\n";
            Assert.Equal(expected, csv);
        }

        [Fact]
        public void Deserialize_WithCustomHeaders()
        {
            // Arrange
            var csv = "名前,年齢,生年月日\r\n" +
                    "田中太郎,30,1993-05-15 00:00:00\r\n" +
                    "山田花子,25,1998-08-20 00:00:00\r\n";

            // Act
            var result = CsvSerializer.Deserialize<TestClassWithCustomHeaders>(csv).ToList();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal("田中太郎", result[0].Name);
            Assert.Equal(30, result[0].Age);
            Assert.Equal(new DateTime(1993, 5, 15), result[0].BirthDate);
            Assert.Equal("山田花子", result[1].Name);
            Assert.Equal(25, result[1].Age);
            Assert.Equal(new DateTime(1998, 8, 20), result[1].BirthDate);
        }

        [Fact]
        public void Deserialize_WithCustomHeaders_ThrowsWhenHeaderMissing()
        {
            // Arrange
            var csv = "名前,年齢\r\n" +  // 生年月日ヘッダーが欠けている
                    "田中太郎,30\r\n";

            // Act & Assert
            var exception = Assert.Throws<CsvSerializationException>(() =>
                CsvSerializer.Deserialize<TestClassWithCustomHeaders>(csv).ToList());
            Assert.Contains("Missing CSV headers: 生年月日", exception.Message);
        }

        [Fact]
        public void Serialize_WithCustomHeaders_AndOptions()
        {
            // Arrange
            var data = new List<TestClassWithCustomHeaders>
            {
                new() { Name = "田中太郎", Age = 30, BirthDate = new DateTime(1993, 5, 15) }
            };
            var options = new CsvSerializerOptions
            {
                Delimiter = ";",
                DateTimeFormat = "yyyy/MM/dd",
                IncludeHeader = true
            };

            // Act
            var csv = CsvSerializer.Serialize(data, options);

            // Assert
            var expected = "名前;年齢;生年月日\r\n" +
                        "田中太郎;30;1993/05/15\r\n";
            Assert.Equal(expected, csv);
        }

        [Fact]
        public void Deserialize_WithExtraHeaders()
        {
            // Arrange
            var csv = "名前,年齢,生年月日,追加項目\r\n" +
                    "田中太郎,30,1993-05-15 00:00:00,値\r\n";

            // Act
            var result = CsvSerializer.Deserialize<TestClassWithCustomHeaders>(csv).ToList();

            // Assert
            Assert.Single(result);
            Assert.Equal("田中太郎", result[0].Name);
            Assert.Equal(30, result[0].Age);
            Assert.Equal(new DateTime(1993, 5, 15), result[0].BirthDate);
        }

        [Fact]
        public void Deserialize_WithMissingValues()
        {
            // Arrange
            var csv = "名前,年齢,生年月日\r\n" +
                    "田中太郎,30\r\n";  // 生年月日の値が欠けている

            // Act
            var result = CsvSerializer.Deserialize<TestClassWithCustomHeaders>(csv).ToList();

            // Assert
            Assert.Single(result);
            Assert.Equal("田中太郎", result[0].Name);
            Assert.Equal(30, result[0].Age);
            Assert.Equal(default, result[0].BirthDate);  // 欠けている値はデフォルト値として扱われる
        }

        [Fact]
        public void Deserialize_WithCompletelyMissingValues()
        {
            // Arrange
            var csv = "名前,年齢,生年月日\r\n" +
                    "\r\n" +  // 空の行は無視される
                    "田中太郎,,\r\n";  // 一部の値が欠けている行

            // Act
            var result = CsvSerializer.Deserialize<TestClassWithCustomHeaders>(csv).ToList();

            // Assert
            Assert.Single(result);
            Assert.Equal("田中太郎", result[0].Name);
            Assert.Equal(0, result[0].Age);
            Assert.Equal(default, result[0].BirthDate);
        }

        // 新しいテストケース：空の行を含むデータのデシリアライズ
        [Fact]
        public void Deserialize_WithEmptyLines()
        {
            // Arrange
            var csv = "名前,年齢,生年月日\r\n" +
                    "\r\n" +  // 空の行
                    "田中太郎,30,1993-05-15 00:00:00\r\n" +
                    "\r\n" +  // 空の行
                    "山田花子,25,1998-08-20 00:00:00\r\n" +
                    "\r\n";   // 空の行

            // Act
            var result = CsvSerializer.Deserialize<TestClassWithCustomHeaders>(csv).ToList();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal("田中太郎", result[0].Name);
            Assert.Equal(30, result[0].Age);
            Assert.Equal(new DateTime(1993, 5, 15), result[0].BirthDate);
            Assert.Equal("山田花子", result[1].Name);
            Assert.Equal(25, result[1].Age);
            Assert.Equal(new DateTime(1998, 8, 20), result[1].BirthDate);
        }
    }

    // 複雑な型とEnumのテスト
    public class ComplexTypesCsvSerializerTests
    {
        public class ComplexTypeClass
        {
            public string Name { get; set; } = "";
            public Dictionary<string, int> Scores { get; set; } = [];
            public List<string> Tags { get; set; } = [];
        }

        public enum UserStatus
        {
            Active,
            Inactive,
            Pending
        }

        public class EnumTestClass
        {
            public string Name { get; set; } = "";
            public UserStatus Status { get; set; }
            public UserStatus? NullableStatus { get; set; }
            public Dictionary<UserStatus, int> StatusCounts { get; set; } = [];
        }

        [Fact]
        public void Serialize_WithEnumTypes()
        {
            // Arrange
            var data = new List<EnumTestClass>
            {
                new() {
                    Name = "田中太郎",
                    Status = UserStatus.Active,
                    NullableStatus = UserStatus.Pending,
                    StatusCounts = new Dictionary<UserStatus, int> {
                        { UserStatus.Active, 10 },
                        { UserStatus.Inactive, 5 }
                    }
                }
            };

            // Act
            var csv = CsvSerializer.Serialize(data);

            // Assert
            var expected = "Name,Status,NullableStatus,Active,Inactive\r\n" +
                        "田中太郎,Active,Pending,10,5\r\n";
            Assert.Equal(expected, csv);
        }

        [Fact]
        public void Deserialize_WithEnumTypes()
        {
            // Arrange
            var csv = "Name,Status,NullableStatus,Active,Inactive\r\n" +
                    "田中太郎,Active,Pending,10,5\r\n";

            // Act
            var result = CsvSerializer.Deserialize<EnumTestClass>(csv).ToList();

            // Assert
            Assert.Single(result);
            var obj = result[0];
            Assert.Equal("田中太郎", obj.Name);
            Assert.Equal(UserStatus.Active, obj.Status);
            Assert.Equal(UserStatus.Pending, obj.NullableStatus);
            Assert.Equal(2, obj.StatusCounts.Count);
            Assert.Equal(10, obj.StatusCounts[UserStatus.Active]);
            Assert.Equal(5, obj.StatusCounts[UserStatus.Inactive]);
        }

        [Fact]
        public void Deserialize_WithEnumTypes_InvalidValue()
        {
            // Arrange
            var csv = "Name,Status,NullableStatus,StatusCounts\r\n" +
                    "田中太郎,InvalidStatus,Pending,\"Active:10;Inactive:5\"\r\n";

            // Act & Assert
            var exception = Assert.Throws<CsvSerializationException>(() =>
                CsvSerializer.Deserialize<EnumTestClass>(csv).ToList());
            Assert.Contains("Failed to convert field 'InvalidStatus' to type", exception.Message);
        }

        [Fact]
        public void Serialize_WithComplexTypes_ThrowsException()
        {
            // Arrange
            var data = new List<ComplexTypeClass>
            {
                new() {
                    Name = "田中太郎",
                    Scores = new Dictionary<string, int> { { "数学", 85 }, { "国語", 90 } },
                    Tags = ["生徒会", "野球部"]
                }
            };

            // Act & Assert
            var exception = Assert.Throws<CsvSerializationException>(() =>
                CsvSerializer.Serialize(data));
            Assert.Contains("is not supported. Only primitive types, string, and DateTime are supported", exception.Message);
        }

        [Fact]
        public void Deserialize_WithComplexTypes_ThrowsException()
        {
            // Arrange
            var csv = "Name,Scores,Tags\r\n田中太郎,{数学:85;国語:90},[生徒会;野球部]\r\n";

            // Act & Assert
            var exception = Assert.Throws<CsvSerializationException>(() =>
                CsvSerializer.Deserialize<ComplexTypeClass>(csv).ToList());
            Assert.Contains("is not supported. Only primitive types, string, and DateTime are supported", exception.Message);
        }

        [Fact]
        public void Serialize_WithNullableEnum()
        {
            // Arrange
            var data = new List<EnumTestClass>
            {
                new() {
                    Name = "田中太郎",
                    Status = UserStatus.Active,
                    NullableStatus = null
                }
            };

            // Act
            var csv = CsvSerializer.Serialize(data);

            // Assert
            var expected = "Name,Status,NullableStatus\r\n" +
                        "田中太郎,Active,\r\n";
            Assert.Equal(expected, csv);
        }

        [Fact]
        public void Deserialize_WithInvalidEnumValue_ThrowsException()
        {
            // Arrange
            var csv = "Name,Status,NullableStatus\r\n" +
                    "田中太郎,Unknown,Pending\r\n";

            // Act & Assert
            var exception = Assert.Throws<CsvSerializationException>(() =>
                CsvSerializer.Deserialize<EnumTestClass>(csv).ToList());
            Assert.Contains("Failed to convert field 'Unknown' to type", exception.Message);
        }

        public class UnsupportedTypeClass
        {
            public string Name { get; set; } = "";
            public List<int> Numbers { get; set; } = [];
        }

        [Fact]
        public void Serialize_WithUnsupportedType_ThrowsException()
        {
            // Arrange
            var data = new List<UnsupportedTypeClass>
            {
                new() { Name = "テスト", Numbers = [1, 2, 3] }
            };

            // Act & Assert
            var exception = Assert.Throws<CsvSerializationException>(() =>
                CsvSerializer.Serialize(data));
            Assert.Contains("is not supported", exception.Message);
        }
    }

    // 新しいテストクラス：エッジケースのテスト
    public class EdgeCasesCsvSerializerTests
    {
        public class NullableTestClass
        {
            public string? Name { get; set; }
            public int? Age { get; set; }
            public DateTime? BirthDate { get; set; }
        }

        [Fact]
        public void Serialize_WithNullValues()
        {
            // Arrange
            var data = new List<NullableTestClass>
            {
                new() { Name = null, Age = null, BirthDate = null }
            };

            // Act
            var csv = CsvSerializer.Serialize(data);

            // Assert
            var expected = "Name,Age,BirthDate\r\n" +
                        ",,\r\n";
            Assert.Equal(expected, csv);
        }

        [Fact]
        public void Deserialize_WithQuotedValues()
        {
            // Arrange
            var csv = "Name,Age,BirthDate\r\n" +
                    "\"田中,太郎\",\"30\",\"1993-05-15 00:00:00\"\r\n";

            // Act
            var result = CsvSerializer.Deserialize<TestClass>(csv).ToList();

            // Assert
            Assert.Single(result);
            Assert.Equal("田中,太郎", result[0].Name);
            Assert.Equal(30, result[0].Age);
            Assert.Equal(new DateTime(1993, 5, 15), result[0].BirthDate);
        }

        [Fact]
        public void Deserialize_WithEscapedQuotes()
        {
            // Arrange
            var csv = "Name,Age,BirthDate\r\n" +
                    "\"田中\"\"太郎\",30,1993-05-15 00:00:00\r\n";

            // Act
            var result = CsvSerializer.Deserialize<TestClass>(csv).ToList();

            // Assert
            Assert.Single(result);
            Assert.Equal("田中\"太郎", result[0].Name);
            Assert.Equal(30, result[0].Age);
            Assert.Equal(new DateTime(1993, 5, 15), result[0].BirthDate);
        }

        [Fact]
        public void Deserialize_EmptyCsv_ThrowsException()
        {
            // Arrange
            var csv = "";

            // Act & Assert
            var exception = Assert.Throws<CsvSerializationException>(() =>
                CsvSerializer.Deserialize<TestClass>(csv).ToList());
            Assert.Contains("CSV data is empty", exception.Message);
        }
    }
}