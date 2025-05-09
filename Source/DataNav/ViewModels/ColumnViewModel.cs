using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DataNav.ViewModels
{
    /// <summary>
    /// ViewModel for a database column
    /// </summary>
    public class ColumnViewModel : INotifyPropertyChanged
    {
        /// <summary>
        /// Event raised when a property changes
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets or sets the column name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the data type
        /// </summary>
        public string DataType { get; set; }

        /// <summary>
        /// Gets or sets whether this column is part of the primary key
        /// </summary>
        public bool IsPrimaryKey { get; set; }

        /// <summary>
        /// Gets or sets whether this column allows null values
        /// </summary>
        public bool IsNullable { get; set; }

        /// <summary>
        /// Gets or sets the parent table
        /// </summary>
        public TableViewModel Table { get; set; }

        /// <summary>
        /// Gets the display text for the column
        /// </summary>
        public string DisplayText => $"{Name} ({DataType}){(IsPrimaryKey ? " PK" : "")}{(IsNullable ? "" : " NOT NULL")}";

        /// <summary>
        /// Raises the PropertyChanged event
        /// </summary>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}