namespace Todo.Presentation.Forms;

using MediatR;

using Microsoft.Extensions.DependencyInjection;

using Todo.Application.Categories.Queries.ListCategories;
using Todo.Application.TodoItems.Commands.ChangeTodoStatus;
using Todo.Application.TodoItems.Commands.DeleteTodoItem;
using Todo.Application.TodoItems.Queries.ListTodoItems;
using Todo.Domain.TodoItems;
using Todo.Presentation.Common;
using Todo.Presentation.Models;

public partial class MainForm : Form
{
    private readonly IServiceProvider _serviceProvider;
    private Dictionary<Guid, string> _categoryNames = new();
    private bool _suppressFilterEvents;

    public MainForm(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        InitializeComponent();
        InitializeFilters();
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        await LoadCategoriesAsync();
        await RefreshTodoItemsAsync();
    }

    private void InitializeFilters()
    {
        statusFilterComboBox.Items.Add("Все статусы");
        statusFilterComboBox.Items.Add("Ожидает");
        statusFilterComboBox.Items.Add("В работе");
        statusFilterComboBox.Items.Add("Выполнена");
        statusFilterComboBox.Items.Add("Отменена");
        SetFilterIndexSilently(statusFilterComboBox, 0);

        categoryFilterComboBox.DisplayMember = "Name";
    }

    private async Task LoadCategoriesAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<ISender>();

        var result = await mediator.Send(new ListCategoriesQuery());

        if (result.IsError)
        {
            UiResults.ShowErrors(result.Errors);
            return;
        }

        _categoryNames = result.Value.ToDictionary(c => c.Id, c => c.Name);

        categoryFilterComboBox.Items.Clear();
        categoryFilterComboBox.Items.Add(new CategoryFilterItem(null, "Все категории"));
        foreach (var category in result.Value)
        {
            categoryFilterComboBox.Items.Add(new CategoryFilterItem(category.Id, category.Name));
        }

        SetFilterIndexSilently(categoryFilterComboBox, 0);
    }

    private async Task RefreshTodoItemsAsync()
    {
        await RunWithBusyStateAsync(async () =>
        {
            using var scope = _serviceProvider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<ISender>();

            Guid? categoryId = null;
            if (categoryFilterComboBox.SelectedItem is CategoryFilterItem categoryFilter && categoryFilter.Id.HasValue)
            {
                categoryId = categoryFilter.Id;
            }

            TodoItemStatus? status = statusFilterComboBox.SelectedIndex switch
            {
                1 => TodoItemStatus.Pending,
                2 => TodoItemStatus.InProgress,
                3 => TodoItemStatus.Completed,
                4 => TodoItemStatus.Cancelled,
                _ => null
            };

            var result = await mediator.Send(new ListTodoItemsQuery(categoryId, status));

            if (result.IsError)
            {
                UiResults.ShowErrors(result.Errors);
                return;
            }

            var rows = result.Value
                .Select(item => TodoItemRow.From(
                    item,
                    _categoryNames.TryGetValue(item.CategoryId, out var name) ? name : "—"))
                .ToList();

            todoItemsGrid.DataSource = null;
            todoItemsGrid.DataSource = rows;
        });
    }

    private async void RefreshButton_Click(object sender, EventArgs e) =>
        await RefreshTodoItemsAsync();

    private async void FilterChanged(object? sender, EventArgs e)
    {
        if (_suppressFilterEvents)
        {
            return;
        }

        await RefreshTodoItemsAsync();
    }

    private void SetFilterIndexSilently(ComboBox comboBox, int index)
    {
        _suppressFilterEvents = true;
        try
        {
            comboBox.SelectedIndex = index;
        }
        finally
        {
            _suppressFilterEvents = false;
        }
    }

    private async void AddButton_Click(object sender, EventArgs e)
    {
        using var scope = _serviceProvider.CreateScope();
        var form = scope.ServiceProvider.GetRequiredService<TodoItemForm>();

        if (form.ShowDialog(this) == DialogResult.OK)
        {
            await RefreshTodoItemsAsync();
        }
    }

    private async void EditButton_Click(object sender, EventArgs e)
    {
        var selected = GetSelectedTodoItem();
        if (selected is null)
        {
            MessageBox.Show("Выберите задачу для редактирования.", "Информация", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var form = scope.ServiceProvider.GetRequiredService<TodoItemForm>();
        form.LoadForEdit(selected.Id);

        if (form.ShowDialog(this) == DialogResult.OK)
        {
            await RefreshTodoItemsAsync();
        }
    }

    private async void DeleteButton_Click(object sender, EventArgs e)
    {
        var selected = GetSelectedTodoItem();
        if (selected is null)
        {
            MessageBox.Show("Выберите задачу для удаления.", "Информация", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!UiResults.ShowConfirmation($"Удалить задачу «{selected.Title}»?"))
        {
            return;
        }

        await RunWithBusyStateAsync(async () =>
        {
            using var scope = _serviceProvider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<ISender>();

            var result = await mediator.Send(new DeleteTodoItemCommand(selected.Id));

            if (result.IsError)
            {
                UiResults.ShowErrors(result.Errors);
                return;
            }

            await RefreshTodoItemsAsync();
        });
    }

    private async void ChangeStatusButton_Click(object sender, EventArgs e)
    {
        var selected = GetSelectedTodoItem();
        if (selected is null)
        {
            MessageBox.Show("Выберите задачу для смены статуса.", "Информация", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new StatusChangeForm();
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        await RunWithBusyStateAsync(async () =>
        {
            using var scope = _serviceProvider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<ISender>();

            var result = await mediator.Send(new ChangeTodoStatusCommand(selected.Id, dialog.SelectedStatus));

            if (result.IsError)
            {
                UiResults.ShowErrors(result.Errors);
                return;
            }

            await RefreshTodoItemsAsync();
        });
    }

    private TodoItemRow? GetSelectedTodoItem()
    {
        if (todoItemsGrid.CurrentRow?.DataBoundItem is TodoItemRow row)
        {
            return row;
        }

        return null;
    }

    private async Task RunWithBusyStateAsync(Func<Task> action)
    {
        try
        {
            UseWaitCursor = true;
            SetButtonsEnabled(false);
            await action();
        }
        finally
        {
            UseWaitCursor = false;
            SetButtonsEnabled(true);
        }
    }

    private void SetButtonsEnabled(bool enabled)
    {
        addButton.Enabled = enabled;
        editButton.Enabled = enabled;
        deleteButton.Enabled = enabled;
        changeStatusButton.Enabled = enabled;
        refreshButton.Enabled = enabled;
    }

    private sealed class CategoryFilterItem
    {
        public Guid? Id { get; }
        public string Name { get; }

        public CategoryFilterItem(Guid? id, string name)
        {
            Id = id;
            Name = name;
        }

        public override string ToString() => Name;
    }
}
