using CommunityToolkit.Mvvm.ComponentModel;
using StickyHomeworks.Services;
using System.Collections.ObjectModel;

namespace StickyHomeworks.Models;

public class Profile : ObservableRecipient
{
    public ObservableCollection<Homework> Homeworks { get; set; } = new();
}