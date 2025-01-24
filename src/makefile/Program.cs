using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;

namespace MakeFile;
internal static class Program
{
    public static void Main()
    {
        string outputFile = "dados_gerados.txt";
        int totalLines = 100_000; // Total de linhas
        int fieldsPerLine = 30; // Número de campos por linha
        int bufferSize = 10_000; // Linhas por buffer para evitar uso excessivo de memória

        GenerateRandomFile(outputFile, totalLines, fieldsPerLine, bufferSize);
        Console.WriteLine($"Arquivo {outputFile} gerado com {totalLines} linhas.");

    }

    public static void GenerateRandomFile(string outputFile, int totalLines, int fieldsPerLine, int bufferSize)
    {
        
        var buffer = new StringBuilder();


        using var writer = new StreamWriter(outputFile, false, Encoding.UTF8);
        for (int i = 1; i <= totalLines; i++)
        {
            string line = GenerateRandomLine(fieldsPerLine);
            var name = new Bogus.Person();
            int value = new Bogus.Randomizer().Number(1, 10_000);

            line = $"{i},{i} {name.FullName} {i},{value},{line}";

            buffer.Append(line).Append(';');

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

    public static string GenerateRandomLine( int fieldsPerLine)
    {
        string[] fields = new string[fieldsPerLine];
        for (int i = 0; i < fieldsPerLine; i++)
        {
            fields[i] = GenerateRandomText(10); // Gera um campo com 10 caracteres
        }
        return string.Join(",", fields);
    }

    public static string GenerateRandomText(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        char[] result = new char[length];
        byte[] uintBuffer = new byte[sizeof(uint)];

        for (int i = 0; i < length; i++)
        {
            RandomNumberGenerator.Fill(uintBuffer);
            uint num = BitConverter.ToUInt32(uintBuffer, 0);
            result[i] = chars[(int)(num % (uint)chars.Length)];
        }
        return new string(result);
    }
}

