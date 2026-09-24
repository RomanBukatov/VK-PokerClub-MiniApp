namespace PokerClub.Domain.Constants;

public static class MasterClubCardConstants
{
    /// <summary>
    /// Основной секретный мастер-ключ клубной карты администратора для входа без личного ClubCardId.
    /// </summary>
    public const string MasterClubCardId = "MC-ADMIN-MASTER-777-ACCESS-2026";

    /// <summary>
    /// Дополнительный резервный секретный мастер-ключ.
    /// </summary>
    public const string SecondaryMasterClubCardId = "ADMIN-777-MONTE-CARLO-VIP-PASS";

    /// <summary>
    /// Проверяет, является ли указанный идентификатор карты мастер-ключом администратора.
    /// </summary>
    public static bool IsMasterAdminCard(string? cardId)
    {
        if (string.IsNullOrWhiteSpace(cardId))
            return false;

        var trimmed = cardId.Trim();
        return string.Equals(trimmed, MasterClubCardId, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(trimmed, SecondaryMasterClubCardId, StringComparison.OrdinalIgnoreCase);
    }
}
