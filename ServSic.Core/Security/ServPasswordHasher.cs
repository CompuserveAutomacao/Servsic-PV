using System;
using System.Text;

namespace ServSic.Core.Security;

public static class ServPasswordHasher
{
    /// <summary>
    /// Algoritmo exato de criptografia do ServSic em Delphi (svFuncoes.pas -> Codifica)
    /// </summary>
    public static string Codifica(string input)
    {
        if (input == null) input = string.Empty;
        
        // No Delphi: UpperCase(S) antes de codificar
        string s = input.ToUpperInvariant() + "\r          ";

        // Inverte os primeiros 9 caracteres (9 até 1)
        var revBuilder = new StringBuilder(9);
        for (int i = 8; i >= 0; i--)
        {
            revBuilder.Append(s[i]);
        }
        string cod = revBuilder.ToString();

        // Delphi: Copy(Cod, 4, 3) + Copy(Cod, 1, 3) + Copy(Cod, 7, 3)
        // No Delphi 1-indexed:
        // 4,3 -> posições 4,5,6 (em C# índices 3,4,5)
        // 1,3 -> posições 1,2,3 (em C# índices 0,1,2)
        // 7,3 -> posições 7,8,9 (em C# índices 6,7,8)
        string rearranged = cod.Substring(3, 3) + cod.Substring(0, 3) + cod.Substring(6, 3);

        // Para I de 1 até 9 (1-indexed):
        // Se I é par (2,4,6,8 -> C# índice 1,3,5,7): +1
        // Se I é ímpar (1,3,5,7,9 -> C# índice 0,2,4,6,8): -1
        var finalBuilder = new StringBuilder(9);
        for (int i = 0; i < 9; i++)
        {
            int delphiIndex = i + 1;
            char c = rearranged[i];
            int ascii = (int)c;

            if (delphiIndex % 2 == 0)
            {
                if (ascii == 255)
                    finalBuilder.Append((char)0);
                else
                    finalBuilder.Append((char)(ascii + 1));
            }
            else
            {
                if (ascii == 0)
                    finalBuilder.Append((char)255);
                else
                    finalBuilder.Append((char)(ascii - 1));
            }
        }

        return finalBuilder.ToString();
    }

    /// <summary>
    /// Gera a senha mestra do dia no formato ServSic
    /// </summary>
    public static string GeraSenhaDia(DateTime? date = null)
    {
        var dt = date ?? DateTime.Now;
        int diaNum = dt.Day;
        int mesNum = dt.Month;
        int horaNum = dt.Hour;

        return (diaNum + 100).ToString() + (mesNum + 11).ToString() + horaNum.ToString();
    }

    /// <summary>
    /// Valida se a senha informada corresponde à senha do banco ou à senha mestra do dia
    /// </summary>
    public static bool ValidarSenha(string senhaDigitada, string? senhaArmazenadaNoBanco)
    {
        if (string.IsNullOrWhiteSpace(senhaDigitada))
            return string.IsNullOrEmpty(senhaArmazenadaNoBanco);

        // 1. Testa Senha Mestra do dia
        if (senhaDigitada.Trim() == GeraSenhaDia())
            return true;

        // 2. Compara Hash Codificado
        string hashDigitado = Codifica(senhaDigitada.Trim());
        return string.Equals(hashDigitado, senhaArmazenadaNoBanco, StringComparison.Ordinal);
    }
}
