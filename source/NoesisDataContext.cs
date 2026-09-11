// Data-binding bridge between the engine and the Noesis UI. The DataContext is an ordinary
// C# object, so this class:
//   * implements System.ComponentModel.INotifyPropertyChanged and a string indexer, which is
//     what the XAML needs for {Binding [key]} (indexer binding);
//   * exposes typed Set/Get helpers;
//   * raises a plain C# `Changed` event for the engine side.
//
// The XAML binds {Binding [key]} to the indexer via INotifyPropertyChanged("Item[]"), and
// binds Command="{Binding [name]}" to a System.Windows.Input.ICommand.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Input;

namespace UnigineApp
{
	public sealed class NoesisDataContext : INotifyPropertyChanged
	{
		private readonly Dictionary<string, object> values = new Dictionary<string, object>();

		public event PropertyChangedEventHandler PropertyChanged;

		// Fired for the engine side (e.g. rotate the sun when "sun_angle_x" changes).
		public event Action<string> Changed;

		// Indexer binding target: {Binding [key]}.
		public object this[string key]
		{
			get
			{
				object value;
				return values.TryGetValue(key, out value) ? value : null;
			}
			set { SetValue(key, value); }
		}

		private void SetValue(string name, object value)
		{
			values[name] = value;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
			Changed?.Invoke(name);
		}

		public void SetFloat(string name, float value) { SetValue(name, value); }

		public float GetFloat(string name)
		{
			object value;
			return values.TryGetValue(name, out value) && value != null ? Convert.ToSingle(value) : 0.0f;
		}

		public void SetInt(string name, int value) { SetValue(name, value); }

		public int GetInt(string name)
		{
			object value;
			return values.TryGetValue(name, out value) && value != null ? Convert.ToInt32(value) : 0;
		}

		public void SetBool(string name, bool value) { SetValue(name, value); }

		public bool GetBool(string name)
		{
			object value;
			return values.TryGetValue(name, out value) && value != null && Convert.ToBoolean(value);
		}

		public void SetString(string name, string value) { SetValue(name, value); }

		public string GetString(string name)
		{
			object value;
			return values.TryGetValue(name, out value) && value != null ? value.ToString() : "";
		}

		public void SetCommand(string name, Action callback) { SetValue(name, new DelegateCommand(callback)); }

		public bool HasProperty(string name) { return values.ContainsKey(name); }
		public void RemoveProperty(string name) { values.Remove(name); }
		public void Clear() { values.Clear(); }
	}

	// Minimal ICommand wrapping an Action.
	public sealed class DelegateCommand : ICommand
	{
		private readonly Action callback;

		public DelegateCommand(Action callback) { this.callback = callback; }

		public event EventHandler CanExecuteChanged;

		public bool CanExecute(object parameter) { return true; }
		public void Execute(object parameter) { callback?.Invoke(); }
	}
}
