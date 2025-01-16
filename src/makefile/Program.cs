using System;
using System.IO;
using System.Text;

class Program
{
    static void Main(string[] args)
    {
        string outputFile = "dados_gerados.txt";
        int totalLines = 100_000; // Total de linhas
        int fieldsPerLine = 30; // Número de campos por linha
        int bufferSize = 10_000; // Linhas por buffer para evitar uso excessivo de memória

        GenerateRandomFile(outputFile, totalLines, fieldsPerLine, bufferSize);
        Console.WriteLine($"Arquivo {outputFile} gerado com {totalLines} linhas.");
    }

    static void GenerateRandomFile(string outputFile, int totalLines, int fieldsPerLine, int bufferSize)
    {
        var random = new Random();
        var buffer = new StringBuilder();
         

        using (var writer = new StreamWriter(outputFile, false, Encoding.UTF8))
        {
            for (int i = 1; i <= totalLines; i++)
            {
                var line = GenerateRandomLine(random, fieldsPerLine);
                var name = new Bogus.Person();
                var value = new Bogus.Randomizer().Number(1, 1000000);

                line = $"{i},{i} {name.FullName} {i},{value},{line}";

                buffer.Append(line).Append(";");
                
                // Grava o buffer no arquivo a cada "bufferSize" linhas
                if (i % bufferSize == 0)
                {
                    writer.Write(buffer.ToString());
                    buffer.Clear();
                    Console.WriteLine($"Linhas gravadas: {i}");
                }
            }

            // Grava as linhas restantes no buffer
            if (buffer.Length > 0)
            {
                writer.Write(buffer.ToString());
            }
        }
    }

    static string GenerateRandomLine(Random random, int fieldsPerLine)
    {
        var fields = new string[fieldsPerLine];
        for (int i = 0; i < fieldsPerLine; i++)
        {
            fields[i] = GenerateRandomText(random, 10); // Gera um campo com 10 caracteres
        }
        return string.Join(",", fields);
    }

    static string GenerateRandomText(Random random, int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var result = new char[length];
        for (int i = 0; i < length; i++)
        {
            result[i] = chars[random.Next(chars.Length)];
        }
        return new string(result);
    }
}

