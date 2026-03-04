using System;
using System.ComponentModel;
using System.Windows.Input;
using Hranitel.Constants;
using Hranitel.Models;
using Hranitel.Services;

namespace Hranitel.ViewModels;

public class BlockedAppViewModel : INotifyPropertyChanged
{
    private readonly MainViewModel _parent;

    public BlockedAppViewModel(BlockedApp model, MainViewModel parent)
    {
        Model = model;
        _parent = parent;
    }

    public BlockedApp Model { get; }

    public string DisplayName => Model.Name;
    public string FullPath => Model.FullPath ?? Model.ProcessName;

    /// <summary> Можно ли удалить (×). false N мин после добавления или при 2-дневном замке. </summary>
    public bool CanRemoveApp =>
        !_parent.Locked &&
        (Model.AddedAt == default || DateTime.Now >= Model.AddedAt.AddMinutes(CooldownConstants.AppRemoveCooldownMinutes));

    /// <summary> Можно ли менять тумблер. Выключен→включить всегда. Включён→выключить — только через N мин после включения. При замке тумблер работает. </summary>
    public bool CanToggleApp =>
        !Model.Enabled
        || Model.ToggledOnAt == default
        || DateTime.Now >= Model.ToggledOnAt.AddMinutes(CooldownConstants.AppToggleCooldownMinutes);

    public bool Enabled
    {
        get => Model.Enabled;
        set
        {
            if (!CanToggleApp)
            {
                OnPropertyChanged(nameof(Enabled));
                if (Model.Enabled && value == false)
                    ToastService.Show("Нельзя выключить в течение 10 минут после включения.", "Хранитель");
                return;
            }
            Model.Enabled = value;
            if (value) Model.ToggledOnAt = DateTime.Now;
            OnPropertyChanged();
            _parent.SaveSettings();
        }
    }

    public ICommand RemoveCommand => new RelayCommand(_ => _parent.RemoveApp(this));

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
