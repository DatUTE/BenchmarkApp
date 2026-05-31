/**
 * @file ViewModelBase.cs
 * @brief Base class for all ViewModels.
 *
 * Inherits from ObservableObject (CommunityToolkit.Mvvm) which provides
 * INotifyPropertyChanged, INotifyPropertyChanging, and source-generated
 * helper methods for property change notification.
 */

using CommunityToolkit.Mvvm.ComponentModel;

namespace Benchmark.UI.ViewModels;

/// <summary>
/// Abstract base for all ViewModels.
/// Provides <see cref="CommunityToolkit.Mvvm.ComponentModel.ObservableObject"/> semantics:
/// efficient property change notification and source-generator support via
/// <c>[ObservableProperty]</c> and <c>[RelayCommand]</c> attributes.
/// </summary>
public abstract class ViewModelBase : ObservableObject { }
