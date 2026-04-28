using Microsoft.AspNetCore.Components;
using MudBlazor;
using VCA.Web.Services;

namespace VCA.Web.Pages.Profile;

/// <summary>
/// Code-behind da página de perfil — próprio (/perfil) e público (/perfil/{userId}).
/// </summary>
public partial class ProfilePage : ComponentBase
{
    [Parameter] public Guid? UserId { get; set; }

    [Inject] private UserHttpService UserService { get; set; } = default!;
    [Inject] private AuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;

    private bool _loading = true;
    private UserProfileDto? _profile;
    private bool _isOwnProfile;

    private int _xpInLevel;
    private int _xpLevelMax;

    // Lista unificada de todos os badges (earned + not-earned)
    private List<BadgeSummaryDto> _allBadges = [];

    // Badges de definição do sistema (código fixo) — mostrados mesmo que não conquistados
    private static readonly string[] KnownBadgeCodes =
        ["ONFIRE", "FIRSTDEPLOY", "VINTAGECONTRIB", "TOPDEV", "KNOWLEDGESEEKER", "SPEEDRUNNER", "QUIZMASTER"];

    protected override async Task OnParametersSetAsync()
    {
        _loading = true;
        _profile = null;

        try
        {
            if (UserId.HasValue)
            {
                // Perfil público
                _profile = await UserService.GetPublicProfileAsync(UserId.Value);
                _isOwnProfile = false;
            }
            else
            {
                // Perfil próprio
                _profile = await UserService.GetMyProfileAsync();
                _isOwnProfile = true;
            }

            if (_profile is not null)
            {
                var (currentInLevel, max) = LevelXpMap.Resolve(_profile.Level, _profile.Xp);
                _xpInLevel = currentInLevel;
                _xpLevelMax = max;

                BuildAllBadges();
            }
        }
        catch { _profile = null; }
        finally { _loading = false; }
    }

    private void BuildAllBadges()
    {
        if (_profile is null) return;

        var earnedMap = _profile.Badges
            .ToDictionary(b => b.Code.ToUpperInvariant(), b => b);

        _allBadges = KnownBadgeCodes.Select(code =>
        {
            if (earnedMap.TryGetValue(code, out var earned))
                return earned with { EarnedAt = earned.EarnedAt };

            return new BadgeSummaryDto(code, BadgeDisplayName(code), null, null);
        }).ToList();
    }

    internal async Task OpenEditModal()
    {
        if (_profile is null) return;

        var parameters = new DialogParameters
        {
            ["CurrentName"]   = _profile.Name,
            ["CurrentAvatar"] = _profile.AvatarUrl
        };

        var dialog = await DialogService.ShowAsync<EditProfileModal>("Editar Perfil", parameters,
            new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseButton = true });

        var result = await dialog.Result;

        if (!result!.Canceled && result.Data is UpdateProfileRequest req)
        {
            var success = await UserService.UpdateProfileAsync(req);
            if (success)
            {
                // Recarrega perfil
                _profile = await UserService.GetMyProfileAsync();
                BuildAllBadges();
                StateHasChanged();
            }
        }
    }

    internal static int LevelNumber(string level) => level switch
    {
        "Rookie"     => 1,
        "Apprentice" => 2,
        "Builder"    => 3,
        "Craftsman"  => 4,
        "Expert"     => 5,
        "VintageDev" => 6,
        _            => 1
    };

    private static string BadgeDisplayName(string code) => code switch
    {
        "ONFIRE"          => "On Fire",
        "FIRSTDEPLOY"     => "First Deploy",
        "VINTAGECONTRIB"  => "Vintage Contributor",
        "TOPDEV"          => "Top Dev",
        "KNOWLEDGESEEKER" => "Knowledge Seeker",
        "SPEEDRUNNER"     => "Speed Runner",
        "QUIZMASTER"      => "Quiz Master",
        _                 => code
    };
}
