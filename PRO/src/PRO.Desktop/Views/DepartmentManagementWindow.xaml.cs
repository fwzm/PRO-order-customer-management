using System.Windows;
using PRO.Desktop.ViewModels;
using PRO.Domain.Entities;

namespace PRO.Desktop.Views;

public partial class DepartmentManagementWindow : Window
{
    private readonly DepartmentTreeViewModel _viewModel;

    public DepartmentManagementWindow(DepartmentTreeViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        Loaded += (s, e) => BuildDepartmentTree();
    }

    private void BuildDepartmentTree()
    {
        var allDepartments = _viewModel.Departments.ToList();
        var topLevel = allDepartments.Where(d => d.ParentId == null || d.ParentId == 0).ToList();
        DepartmentTree.ItemsSource = topLevel.Select(d => BuildTreeNode(d, allDepartments)).ToList();
    }

    private DepartmentTreeNode BuildTreeNode(Department dept, List<Department> all)
    {
        var node = new DepartmentTreeNode
        {
            Id = dept.Id,
            Name = dept.Name,
            ParentId = dept.ParentId,
            ManagerName = dept.ManagerId > 0 ? $"负责人ID: {dept.ManagerId}" : ""
        };
        var children = all.Where(d => d.ParentId == dept.Id).ToList();
        foreach (var child in children)
        {
            node.Children.Add(BuildTreeNode(child, all));
        }
        return node;
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        await _viewModel.RefreshCommand.ExecuteAsync(null);
        BuildDepartmentTree();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    public class DepartmentTreeNode
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int? ParentId { get; set; }
        public string? ManagerName { get; set; }
        public List<DepartmentTreeNode> Children { get; set; } = [];
    }
}
