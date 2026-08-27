using System;
using System.Text.Json;
public class Test { public string ChatID { get; set; } = "123"; }
class Program { static void Main() { 
    var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    Console.WriteLine(JsonSerializer.Serialize(new Test(), options));
} }
