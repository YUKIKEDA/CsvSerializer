namespace CsvSerializer.UnitTests
{
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

    public class CsvSerializerTests
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
            var exception = Assert.Throws<InvalidOperationException>(() =>
                CsvSerializer.Deserialize<TestClassWithCustomHeaders>(csv).ToList());
            Assert.Contains("Missing CSV headers: 生年月日", exception.Message);
        }

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

        public class ComplexTypeClass
        {
            public string Name { get; set; } = "";
            public Dictionary<string, int> Scores { get; set; } = new();
            public List<string> Tags { get; set; } = new();
        }

        [Fact]
        public void Serialize_WithComplexTypes_ThrowsException()
        {
            // Arrange
            var data = new List<ComplexTypeClass>
            {
                new ComplexTypeClass
                {
                    Name = "田中太郎",
                    Scores = new Dictionary<string, int> { { "数学", 85 }, { "国語", 90 } },
                    Tags = new List<string> { "生徒会", "野球部" }
                }
            };

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() =>
                CsvSerializer.Serialize(data));
            Assert.Contains("Property 'Scores' of type 'Dictionary`2' is not supported", exception.Message);
        }

        [Fact]
        public void Deserialize_WithComplexTypes_ThrowsException()
        {
            // Arrange
            var csv = "Name,Scores,Tags\r\n田中太郎,{数学:85;国語:90},[生徒会;野球部]\r\n";

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() =>
                CsvSerializer.Deserialize<ComplexTypeClass>(csv).ToList());
            Assert.Contains("Property 'Scores' of type 'Dictionary`2' is not supported", exception.Message);
        }
    }
}