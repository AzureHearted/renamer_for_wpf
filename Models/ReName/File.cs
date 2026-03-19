using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Shapes;

namespace ReNamer.Models.ReName
{
    public partial class ReNameFile : ObservableObject
    {
        [ObservableProperty]
        private string _path = String.Empty;

        [ObservableProperty]
        private string _name = String.Empty;

        [ObservableProperty]
        private string _dir = String.Empty;

        [ObservableProperty]
        private string _ext = String.Empty;


        [ObservableProperty]
        private bool _isDirectory;


        /// <summary>
        /// 是否允许被修改
        /// </summary>
        [ObservableProperty]
        private bool _enable = true;

        /// <summary>
        /// 标记祖先目录是否被修改
        /// </summary>
        [ObservableProperty]
        private bool _parentDirRenamed = false;

        /// <summary>
        /// 新名称
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(NewNameNoExt))]
        [NotifyPropertyChangedFor(nameof(NewPath))]
        private string _newName = string.Empty;

        /// <summary>
        /// 冲突标识符
        /// </summary>
        [ObservableProperty]
        private bool _isConflict;

        /// <summary>
        /// 判断是否能正常修改标识符
        /// </summary>
        public bool IsOK => Name != NewName && !IsConflict;

        // --- 只读逻辑属性：基于内存计算，不涉及 I/O ---

        public string NameNoExt => IsDirectory ? Name : System.IO.Path.GetFileNameWithoutExtension(Name);

        public string NewNameNoExt => IsDirectory ? NewName : System.IO.Path.GetFileNameWithoutExtension(NewName);

        public string NewPath => System.IO.Path.Combine(Dir, NewName);

        // 冲突检查建议作为“计算属性”，如果性能要求极高，也可以改成字段
        //public bool IsConflict => Path != NewPath && NewPathExist;

        // 这个状态建议在执行重命名检查逻辑时，由后台线程统一计算后赋值
        [ObservableProperty]
        private bool _newPathExist;

        [ObservableProperty]
        private bool _pathExist;

        /// <summary>
        /// 更新到新名称
        /// </summary>
        public void UpdateToNewName(string? path = null)
        {
            path = path != null ? path : NewPath;
            Path = path;
            Name = System.IO.Path.GetFileName(path);
            Ext =
               !IsDirectory ? System.IO.Path.GetExtension(Path).Replace(".", "")  // 去除掉 .
               : "";
        }

        public ReNameFile(string path)
        {
            Path = path;
            Name = System.IO.Path.GetFileName(path);
            Dir = System.IO.Path.GetDirectoryName(path) ?? String.Empty;

            if (Directory.Exists(path)) IsDirectory = true;

            Ext =
                !IsDirectory ? System.IO.Path.GetExtension(path).Replace(".", "")  // 去除掉 .
                : "";


            NewName = Name;

            PathExist = true;
            NewPathExist = true;
        }
    }
}
