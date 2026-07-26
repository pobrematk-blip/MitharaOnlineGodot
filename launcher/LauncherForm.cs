using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace Mithara.Launcher;

public sealed class LauncherForm : Form
{
    private const string RepositoryOwner = "pobrematk-blip";
    private const string RepositoryName = "MitharaOnlineGodot";
    private const string ManifestAssetName = "manifest.json";
    private const string ArchiveAssetName = "MitharaOnline_Update.zip";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly HttpClient _http = new();
    private readonly Label _status = new();
    private readonly Label _version = new();
    private readonly ProgressBar _progress = new();
    private readonly Button _playButton = new();
    private readonly Button _retryButton = new();
    private readonly Button _shortcutButton = new();
    private UpdateManifest? _activeManifest;

    private string GameDirectory => Path.Combine(AppContext.BaseDirectory, "Game");
    private string LocalManifestPath => Path.Combine(GameDirectory, ManifestAssetName);

    public LauncherForm()
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("MitharaLauncher/1.0");
        _http.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
        {
            NoCache = true,
            NoStore = true,
        };
        _http.DefaultRequestHeaders.Pragma.ParseAdd("no-cache");
        _http.Timeout = TimeSpan.FromMinutes(30);

        Text = "Mithara Online Launcher";
        Icon = LoadEmbeddedIcon("MitharaLauncherIcon.ico") ?? Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? Icon;
        ClientSize = new Size(760, 430);
        MinimumSize = new Size(680, 390);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(8, 10, 18);
        BackgroundImage = LoadEmbeddedImage("MitharaLauncherBackground.png");
        BackgroundImageLayout = ImageLayout.Stretch;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10f);

        BuildInterface();
        Shown += async (_, _) =>
        {
            TryCreateDesktopShortcut(silent: true);
            await CheckAndUpdateAsync();
        };
        FormClosed += (_, _) => _http.Dispose();
    }

    private void BuildInterface()
    {
        var title = new Label
        {
            Text = "MITHARA ONLINE",
            Font = new Font("Georgia", 30f, FontStyle.Bold),
            ForeColor = Color.FromArgb(220, 178, 74),
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(36, 36),
        };

        var subtitle = new Label
        {
            Text = "CONFLITO DE RAÇAS",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.FromArgb(156, 132, 205),
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(42, 92),
        };

        var separator = new Panel
        {
            BackColor = Color.FromArgb(75, 65, 105),
            Location = new Point(38, 130),
            Size = new Size(684, 1),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };

        var demoBadge = new Label
        {
            Text = "DEMONSTRACAO EM DESENVOLVIMENTO",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(255, 218, 112),
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(42, 148),
        };

        var demoMessage = new Label
        {
            Text = "Mithara Online esta em desenvolvimento ativo. Esta versao e uma demonstracao jogavel do mundo, dos sistemas e das aventuras que estamos construindo.",
            Font = new Font("Segoe UI", 10.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(235, 233, 222),
            BackColor = Color.Transparent,
            Location = new Point(40, 172),
            Size = new Size(680, 44),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };

        _status.Text = "Preparando launcher...";
        _status.Location = new Point(40, 232);
        _status.Size = new Size(680, 34);
        _status.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _status.ForeColor = Color.FromArgb(220, 220, 228);
        _status.BackColor = Color.Transparent;

        _version.Text = "Versão local: verificando";
        _version.Location = new Point(40, 270);
        _version.Size = new Size(680, 24);
        _version.ForeColor = Color.FromArgb(145, 148, 165);
        _version.BackColor = Color.Transparent;

        _progress.Location = new Point(40, 306);
        _progress.Size = new Size(680, 24);
        _progress.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _progress.Style = ProgressBarStyle.Continuous;

        _retryButton.Text = "Verificar novamente";
        _retryButton.Location = new Point(40, 352);
        _retryButton.Size = new Size(165, 46);
        _retryButton.BackColor = Color.FromArgb(24, 25, 35);
        _retryButton.ForeColor = Color.White;
        _retryButton.FlatStyle = FlatStyle.Flat;
        _retryButton.FlatAppearance.BorderColor = Color.FromArgb(126, 97, 47);
        _retryButton.Enabled = false;
        _retryButton.Click += async (_, _) => await CheckAndUpdateAsync();

        _shortcutButton.Text = "Criar atalho";
        _shortcutButton.Location = new Point(220, 352);
        _shortcutButton.Size = new Size(145, 46);
        _shortcutButton.BackColor = Color.FromArgb(24, 25, 35);
        _shortcutButton.ForeColor = Color.White;
        _shortcutButton.FlatStyle = FlatStyle.Flat;
        _shortcutButton.FlatAppearance.BorderColor = Color.FromArgb(126, 97, 47);
        _shortcutButton.Click += (_, _) => TryCreateDesktopShortcut(silent: false);

        _playButton.Text = "JOGAR";
        _playButton.Location = new Point(550, 352);
        _playButton.Size = new Size(170, 46);
        _playButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _playButton.BackColor = Color.FromArgb(156, 100, 26);
        _playButton.ForeColor = Color.White;
        _playButton.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
        _playButton.FlatStyle = FlatStyle.Flat;
        _playButton.FlatAppearance.BorderColor = Color.FromArgb(226, 181, 82);
        _playButton.Enabled = false;
        _playButton.Click += (_, _) => LaunchGame();

        Controls.AddRange(new Control[]
        {
            title, subtitle, separator, demoBadge, demoMessage, _status, _version, _progress, _retryButton, _shortcutButton, _playButton,
        });
    }

    private async Task CheckAndUpdateAsync()
    {
        SetBusy(true);
        _progress.Value = 0;
        _status.Text = "Verificando atualizações no GitHub...";

        try
        {
            UpdateManifest? local = await ReadManifestAsync(LocalManifestPath);
            _version.Text = $"Versão local: {local?.Version ?? "não instalada"}";

            string latestDownloadBase = $"https://github.com/{RepositoryOwner}/{RepositoryName}/releases/latest/download";
            UpdateManifest remote = await GetJsonAsync<UpdateManifest>(WithCacheBust($"{latestDownloadBase}/{ManifestAssetName}"))
                ?? throw new InvalidOperationException("O manifesto da atualização é inválido.");
            _activeManifest = remote;

            string gameExecutable = SafePath(GameDirectory, remote.GameExecutable);
            bool arquivosValidos = local?.Version == remote.Version
                && await ValidateInstalledFilesAsync(remote);
            bool needsUpdate = !arquivosValidos;
            if (needsUpdate)
            {
                string archiveName = string.IsNullOrWhiteSpace(remote.Archive) ? ArchiveAssetName : remote.Archive;
                GitHubAsset archiveAsset = new()
                {
                    Name = archiveName,
                    DownloadUrl = WithCacheBust($"{latestDownloadBase}/{archiveName}"),
                    Size = 0,
                };
                await InstallUpdateAsync(remote, archiveAsset);
            }

            _version.Text = $"Versão instalada: {remote.Version}";
            _status.Text = "Jogo atualizado e pronto para iniciar.";
            _progress.Value = 100;
            _playButton.Enabled = File.Exists(gameExecutable);
        }
        catch (Exception ex)
        {
            _status.Text = $"Não foi possível atualizar: {ex.Message}";
            // Nunca inicia um cliente possivelmente antigo ou parcialmente atualizado.
            _playButton.Enabled = false;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task<bool> ValidateInstalledFilesAsync(UpdateManifest manifest)
    {
        if (manifest.Files.Count == 0) return false;

        _status.Text = "Verificando integridade dos arquivos instalados...";
        for (int i = 0; i < manifest.Files.Count; i++)
        {
            ManifestFile file = manifest.Files[i];
            string path = SafePath(GameDirectory, file.Path);
            if (!File.Exists(path)) return false;

            var info = new FileInfo(path);
            if (info.Length != file.Size) return false;

            string hash = await ComputeSha256Async(path);
            if (!hash.Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
                return false;

            int percent = (i + 1) * 100 / manifest.Files.Count;
            _progress.Value = percent;
            _status.Text = $"Verificando arquivos instalados... {percent}%";
        }

        return true;
    }

    private async Task InstallUpdateAsync(UpdateManifest manifest, GitHubAsset archiveAsset)
    {
        if (IsGameRunning(manifest.GameExecutable))
            throw new InvalidOperationException("Feche o jogo antes de atualizar.");

        string tempRoot = Path.Combine(Path.GetTempPath(), $"mithara-update-{Guid.NewGuid():N}");
        string archivePath = Path.Combine(tempRoot, manifest.Archive);
        string staging = Path.Combine(tempRoot, "staging");
        Directory.CreateDirectory(tempRoot);
        Directory.CreateDirectory(staging);

        try
        {
            _status.Text = "Baixando atualização...";
            await DownloadAsync(archiveAsset.DownloadUrl, archivePath, archiveAsset.Size);

            _status.Text = "Validando pacote...";
            string archiveHash = await ComputeSha256Async(archivePath);
            if (!archiveHash.Equals(manifest.ArchiveSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("O SHA-256 do pacote não confere.");

            _status.Text = "Extraindo arquivos...";
            ZipFile.ExtractToDirectory(archivePath, staging, true);
            await ValidateFilesAsync(staging, manifest.Files);

            _status.Text = "Instalando atualização...";
            Directory.CreateDirectory(GameDirectory);
            foreach (ManifestFile file in manifest.Files)
            {
                string source = SafePath(staging, file.Path);
                string destination = SafePath(GameDirectory, file.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(source, destination, true);
            }

            foreach (string obsolete in manifest.Remove)
            {
                string path = SafePath(GameDirectory, obsolete);
                if (File.Exists(path)) File.Delete(path);
            }

            await File.WriteAllTextAsync(LocalManifestPath, JsonSerializer.Serialize(manifest, JsonOptions));
        }
        finally
        {
            try { Directory.Delete(tempRoot, true); } catch { }
        }
    }

    private async Task DownloadAsync(string url, string destination, long expectedSize)
    {
        using HttpResponseMessage response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        long total = response.Content.Headers.ContentLength ?? expectedSize;

        await using Stream input = await response.Content.ReadAsStreamAsync();
        await using FileStream output = new(destination, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 128, true);
        byte[] buffer = new byte[1024 * 128];
        long received = 0;
        int read;
        while ((read = await input.ReadAsync(buffer)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read));
            received += read;
            if (total > 0)
            {
                int percent = (int)Math.Clamp(received * 100L / total, 0, 100);
                _progress.Value = percent;
                _status.Text = $"Baixando atualização... {percent}% ({received / 1048576} MB de {total / 1048576} MB)";
            }
        }
    }

    private static async Task ValidateFilesAsync(string root, IEnumerable<ManifestFile> files)
    {
        foreach (ManifestFile file in files)
        {
            string path = SafePath(root, file.Path);
            if (!File.Exists(path)) throw new InvalidDataException($"Arquivo ausente: {file.Path}");
            var info = new FileInfo(path);
            if (info.Length != file.Size) throw new InvalidDataException($"Tamanho inválido: {file.Path}");
            string hash = await ComputeSha256Async(path);
            if (!hash.Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Arquivo corrompido: {file.Path}");
        }
    }

    private void LaunchGame()
    {
        if (!TryFindInstalledGame(out string executable))
        {
            _status.Text = "Executável do jogo não encontrado.";
            return;
        }

        Process.Start(new ProcessStartInfo(executable)
        {
            WorkingDirectory = Path.GetDirectoryName(executable)!,
            UseShellExecute = true,
        });
        WindowState = FormWindowState.Minimized;
    }

    private bool TryFindInstalledGame(out string executable)
    {
        string name = _activeManifest?.GameExecutable ?? "MitharaOnlineTeste.exe";
        executable = SafePath(GameDirectory, name);
        return File.Exists(executable);
    }

    private static bool IsGameRunning(string executable) => Process
        .GetProcessesByName(Path.GetFileNameWithoutExtension(executable))
        .Length > 0;

    private static Image? LoadEmbeddedImage(string resourceName)
    {
        using Stream? stream = typeof(LauncherForm).Assembly.GetManifestResourceStream(resourceName);
        return stream is null ? null : Image.FromStream(stream);
    }

    private static Icon? LoadEmbeddedIcon(string resourceName)
    {
        using Stream? stream = typeof(LauncherForm).Assembly.GetManifestResourceStream(resourceName);
        return stream is null ? null : (Icon)new Icon(stream).Clone();
    }

    private void TryCreateDesktopShortcut(bool silent)
    {
        try
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string shortcutPath = Path.Combine(desktop, "Mithara Online.lnk");
            string launcherPath = Application.ExecutablePath;

            if (silent && File.Exists(shortcutPath))
                return;

            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
                throw new InvalidOperationException("Criador de atalhos do Windows indisponivel.");

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = launcherPath;
            shortcut.WorkingDirectory = AppContext.BaseDirectory;
            shortcut.IconLocation = $"{launcherPath},0";
            shortcut.Description = "Atualizar e jogar Mithara Online";
            shortcut.Save();

            if (!silent)
                _status.Text = "Atalho criado na Area de Trabalho.";
        }
        catch (Exception ex)
        {
            if (!silent)
                _status.Text = $"Nao foi possivel criar o atalho: {ex.Message}";
        }
    }

    private static string WithCacheBust(string url)
    {
        string separator = url.Contains('?') ? "&" : "?";
        return $"{url}{separator}t={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
    }

    private async Task<T?> GetJsonAsync<T>(string url)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        string json = await _http.GetStringAsync(url, timeout.Token);
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    private static async Task<UpdateManifest?> ReadManifestAsync(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            string json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<UpdateManifest>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string> ComputeSha256Async(string path)
    {
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 128, true);
        byte[] hash = await SHA256.HashDataAsync(stream);
        return Convert.ToHexString(hash);
    }

    private static string SafePath(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative))
            throw new InvalidDataException("Caminho inválido no manifesto.");

        string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string fullPath = Path.GetFullPath(Path.Combine(fullRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("O manifesto tentou acessar um caminho fora do jogo.");
        return fullPath;
    }

    private void SetBusy(bool busy)
    {
        _retryButton.Enabled = !busy;
        _shortcutButton.Enabled = !busy;
        if (busy) _playButton.Enabled = false;
        UseWaitCursor = busy;
    }
}
