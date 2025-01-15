using System;
using System.IO;
using System.Text;
using RabbitMQ.Client;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

class Program
{
    static void Main(string[] args)
    { 
        
       BenchmarkRunner.Run<Execute>();
       
    }

} 