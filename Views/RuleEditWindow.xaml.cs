using ReNamer.Models.ReName;
using ReNamer.ViewModels;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ReNamer.Views
{
    /// <summary>
    /// RuleEditWindow.xaml 的交互逻辑
    /// </summary>
    public partial class RuleEditWindow
    {
        public RuleEditWindow(RuleEditWindowViewModel vm)
        {
            InitializeComponent();

            DataContext = vm;

            vm.OnAddRule += (e) => this.DialogResult = true;
            vm.OnSaveRule += (e) => this.DialogResult = true;
            vm.OnCancel += () => this.DialogResult = false;
        }
    }
}
