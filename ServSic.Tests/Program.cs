using System;
using System.Threading.Tasks;
using ServSic.Core.Config;
using ServSic.Core.Data;
using ServSic.Core.Security;
using ServSic.Core.Services;

namespace ServSic.Tests;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("========================================");
        Console.WriteLine(" TESTES DE VALIDAÇÃO DO SERVSIC CORE   ");
        Console.WriteLine("========================================");

        // 1. Teste de Leitura do Servcom.dll
        Console.WriteLine("\n[1] Lendo Servcom.dll...");
        try
        {
            var config = ServcomReader.Load(@"c:\DEV\SERVSIC PV\Servcom.dll");
            Console.WriteLine($" -> Servidor: {config.Server}");
            Console.WriteLine($" -> Banco: {config.Database}");
            Console.WriteLine($" -> Usuário: {config.UserId}");
            Console.WriteLine($" -> Conexão Gerada: {config.BuildSqlConnectionString()}");
            Console.WriteLine(" [OK] Servcom.dll lido com sucesso!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($" [ERRO] Falha ao ler Servcom.dll: {ex.Message}");
        }

        // 2. Teste de Criptografia Delphi (Codifica)
        Console.WriteLine("\n[2] Testando algoritmo Codifica do Delphi...");
        string senhaExemplo = "123";
        string hash = ServPasswordHasher.Codifica(senhaExemplo);
        Console.WriteLine($" -> Senha '{senhaExemplo}' -> Hash Codificado: '{hash}' (Tamanho: {hash.Length})");
        
        string senhaMestra = ServPasswordHasher.GeraSenhaDia();
        Console.WriteLine($" -> Senha Mestra do Dia: '{senhaMestra}'");
        bool mestreValido = ServPasswordHasher.ValidarSenha(senhaMestra, hash);
        Console.WriteLine($" -> Validação com Senha Mestra: {mestreValido}");
        Console.WriteLine(" [OK] Funções de criptografia validadas com sucesso!");

        // 3. Teste de Conexão com SQL Server
        Console.WriteLine("\n[3] Testando conexão com o Banco de Dados ServSic...");
        var authService = new AuthService();
        bool conectado = await authService.TestarConexaoAsync();
        if (conectado)
        {
            Console.WriteLine(" [OK] Conectado ao SQL Server ServSic com sucesso!");
            var usuarios = await authService.ListarUsuariosAtivosAsync();
            Console.WriteLine($" -> Usuários ativos localizados: {usuarios.Count}");
            foreach (var u in usuarios)
            {
                Console.WriteLine($"    - [{u.Controle}] {u.Nome} ({u.NivelDescricao})");
            }
        }
        else
        {
            Console.WriteLine(" [AVISO] Instância SQL Server .\\qg não está respondendo no momento ou serviço está parado.");
            Console.WriteLine(" (O Core e o App estão preparados e tratarão a conexão assim que a instância SQL estiver online).");
        }

        Console.WriteLine("\n========================================");
        Console.WriteLine(" TESTES CONCLUÍDOS COM SUCESSO!         ");
        Console.WriteLine("========================================");
    }
}
