using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;
using VCA.Web.Services;

namespace VCA.Web.Pages.Profile;

/// <summary>
/// Code-behind do modal de edição de perfil.
/// Faz upload do avatar como base64 → Supabase Storage via API e salva o nome.
/// </summary>
public partial class EditProfileModal : ComponentBase
{
    [CascadingParameter] private MudDialogInstance MudDialog { get; set; } = default!;

    [Parameter] public string CurrentName { get; set; } = string.Empty;
    [Parameter] public string? CurrentAvatar { get; set; }

    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private UserHttpService UserService { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;

    private string _name = string.Empty;
    private string? _avatarPreview;
    private string? _avatarBase64;
    private string? _avatarContentType;

    private string? _nameError;
    private string? _avatarError;

    private bool _saving;
    private bool _uploadingAvatar;

    private const long MaxFileSizeBytes = 2 * 1024 * 1024; // 2 MB
    private static readonly string[] AllowedTypes = ["image/png", "image/jpeg", "image/webp"];

    private bool IsValid =>
        !string.IsNullOrWhiteSpace(_name) &&
        _name.Length <= 60 &&
        !_uploadingAvatar &&
        !_saving;

    protected override void OnParametersSet()
    {
        _name = CurrentName;
        _avatarPreview = CurrentAvatar;
    }

    private async Task HandleAvatarSelectedAsync(InputFileChangeEventArgs e)
    {
        _avatarError = null;
        var file = e.File;

        if (!AllowedTypes.Contains(file.ContentType))
        {
            _avatarError = "Formato inválido. Use PNG, JPEG ou WebP.";
            return;
        }

        if (file.Size > MaxFileSizeBytes)
        {
            _avatarError = "A imagem deve ter no máximo 2 MB.";
            return;
        }

        _uploadingAvatar = true;
        StateHasChanged();

        try
        {
            using var stream = file.OpenReadStream(MaxFileSizeBytes);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            var bytes = ms.ToArray();

            _avatarBase64 = Convert.ToBase64String(bytes);
            _avatarContentType = file.ContentType;

            // Mostra preview local imediatamente
            _avatarPreview = $"data:{file.ContentType};base64,{_avatarBase64}";
        }
        catch
        {
            _avatarError = "Erro ao ler o arquivo. Tente novamente.";
        }
        finally
        {
            _uploadingAvatar = false;
        }
    }

    private async Task SaveAsync()
    {
        _nameError = null;

        if (string.IsNullOrWhiteSpace(_name))
        { _nameError = "O nome não pode ser vazio."; return; }

        if (_name.Length > 60)
        { _nameError = "O nome deve ter no máximo 60 caracteres."; return; }

        _saving = true;
        try
        {
            string? finalAvatarUrl = CurrentAvatar;

            // Upload do avatar se selecionado
            if (!string.IsNullOrEmpty(_avatarBase64))
            {
                var uploadPayload = new AvatarUploadRequest(_avatarBase64, _avatarContentType!);
                var uploadResp = await Http.PostAsJsonAsync("api/users/me/avatar", uploadPayload);

                if (uploadResp.IsSuccessStatusCode)
                {
                    var result = await uploadResp.Content.ReadFromJsonAsync<AvatarUploadResponse>();
                    finalAvatarUrl = result?.AvatarUrl;
                }
                else
                {
                    Snackbar.Add("Falha ao fazer upload do avatar.", Severity.Warning);
                    // Continua com avatar antigo
                }
            }

            MudDialog.Close(DialogResult.Ok(new UpdateProfileRequest(_name.Trim(), finalAvatarUrl)));
        }
        catch
        {
            Snackbar.Add("Erro ao salvar o perfil.", Severity.Error);
        }
        finally
        {
            _saving = false;
        }
    }

    private void Cancel() => MudDialog.Cancel();
}

file record AvatarUploadRequest(string Base64Content, string ContentType);
file record AvatarUploadResponse(string AvatarUrl);
