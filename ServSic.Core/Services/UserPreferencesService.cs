using System;
using System.IO;

namespace ServSic.Core.Services;

/// <summary>
/// Serviço de persistência local de preferências do usuário na máquina (AppData/ServsicPV).
/// </summary>
public class UserPreferencesService
{
    private static readonly string FolderPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ServsicPV"
    );

    private static readonly string LastUserFilePath = Path.Combine(FolderPath, "last_user.txt");

    /// <summary>
    /// Retorna o nome do último usuário logado com sucesso no sistema nesta estação.
    /// </summary>
    public string? ObterUltimoUsuario()
    {
        try
        {
            if (File.Exists(LastUserFilePath))
            {
                string nome = File.ReadAllText(LastUserFilePath).Trim();
                if (!string.IsNullOrWhiteSpace(nome))
                {
                    return nome;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[UserPreferencesService] Erro ao ler último usuário: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Grava o nome do último usuário logado com sucesso.
    /// </summary>
    public void SalvarUltimoUsuario(string? nomeUsuario)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(nomeUsuario)) return;

            if (!Directory.Exists(FolderPath))
            {
                Directory.CreateDirectory(FolderPath);
            }

            File.WriteAllText(LastUserFilePath, nomeUsuario.Trim());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[UserPreferencesService] Erro ao salvar último usuário: {ex.Message}");
        }
    }
}
