namespace Hranitel.Constants;

public static class CooldownConstants
{
    /// <summary> Минут до разблокировки главного тумблера и расписания после включения блокировки. </summary>
    public const int BlockCooldownMinutes = 10;

    /// <summary> Минут до возможности удалить приложение после добавления. </summary>
    public const int AppRemoveCooldownMinutes = 10;

    /// <summary> Минут до возможности выключить тумблер приложения после включения. </summary>
    public const int AppToggleCooldownMinutes = 10;

    /// <summary> Дней блокировки настроек по кнопке «Блок на 2 дня». </summary>
    public const int LockDays = 2;

    /// <summary> Интервал опроса кулдаунов (секунды). </summary>
    public const int CooldownPollIntervalSeconds = 30;
}
