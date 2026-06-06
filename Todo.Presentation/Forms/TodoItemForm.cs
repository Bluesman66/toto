namespace Todo.Presentation.Forms;

using MediatR;

using Microsoft.Extensions.DependencyInjection;

using Todo.Application.Categories.Queries.ListCategories;
using Todo.Application.TodoItems.Commands.CreateTodoItem;
using Todo.Application.TodoItems.Commands.UpdateTodoItem;
using Todo.Application.TodoItems.Queries.GetTodoItemById;
using Todo.Domain.Categories;
using Todo.Domain.TodoItems;
using Todo.Presentation.Common;

public partial class TodoItemForm : Form
{
    private readonly IServiceProvider _serviceProvider;
    private Guid? _editId;
    private List<Category> _categories = new();

    public TodoItemForm(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        InitializeComponent();
        InitializeComboBoxes();
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        await LoadCategoriesAsync();
    }

    public void LoadForEdit(Guid id)
    {
        _editId = id;
        Text = "Редактирование задачи";
        statusComboBox.Enabled = true;
        _ = LoadTodoItemAsync(id);
    }

    private void InitializeComboBoxes()
    {
        priorityComboBox.Items.AddRange(new object[] { "Низкий", "Средний", "Высокий" });
        priorityComboBox.SelectedIndex = 1;

        statusComboBox.Items.AddRange(new object[] { "Ожидает", "В работе", "Выполнена", "Отменена" });
        statusComboBox.SelectedIndex = 0;

        dueDatePicker.Format = DateTimePickerFormat.Short;
        dueDatePicker.ShowCheckBox = true;
        dueDatePicker.Checked = false;
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

        _categories = result.Value;
        categoryComboBox.DataSource = null;
        categoryComboBox.DisplayMember = "Name";
        categoryComboBox.ValueMember = "Id";
        categoryComboBox.DataSource = _categories;

        if (_categories.Count > 0)
        {
            categoryComboBox.SelectedIndex = 0;
        }
    }

    private async Task LoadTodoItemAsync(Guid id)
    {
        using var scope = _serviceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<ISender>();

        var result = await mediator.Send(new GetTodoItemByIdQuery(id));

        if (result.IsError)
        {
            UiResults.ShowErrors(result.Errors);
            DialogResult = DialogResult.Cancel;
            Close();
            return;
        }

        var item = result.Value;
        titleTextBox.Text = item.Title;
        descriptionTextBox.Text = item.Description ?? string.Empty;
        categoryComboBox.SelectedValue = item.CategoryId;
        priorityComboBox.SelectedIndex = (int)item.Priority;
        statusComboBox.SelectedIndex = (int)item.Status;

        if (item.DueDate.HasValue)
        {
            dueDatePicker.Checked = true;
            dueDatePicker.Value = item.DueDate.Value;
        }
        else
        {
            dueDatePicker.Checked = false;
        }
    }

    private async void SaveButton_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(titleTextBox.Text))
        {
            MessageBox.Show("Введите название задачи.", "Проверка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (categoryComboBox.SelectedValue is not Guid categoryId)
        {
            MessageBox.Show("Выберите категорию.", "Проверка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var priority = (TodoPriority)priorityComboBox.SelectedIndex;
        DateTime? dueDate = dueDatePicker.Checked ? dueDatePicker.Value.Date : null;
        var description = string.IsNullOrWhiteSpace(descriptionTextBox.Text) ? null : descriptionTextBox.Text.Trim();

        try
        {
            saveButton.Enabled = false;
            UseWaitCursor = true;

            using var scope = _serviceProvider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<ISender>();

            if (_editId is null)
            {
                var createResult = await mediator.Send(new CreateTodoItemCommand(
                    titleTextBox.Text.Trim(),
                    categoryId,
                    priority,
                    dueDate,
                    description));

                if (createResult.IsError)
                {
                    UiResults.ShowErrors(createResult.Errors);
                    return;
                }
            }
            else
            {
                var updateResult = await mediator.Send(new UpdateTodoItemCommand(
                    _editId.Value,
                    titleTextBox.Text.Trim(),
                    categoryId,
                    priority,
                    dueDate,
                    description));

                if (updateResult.IsError)
                {
                    UiResults.ShowErrors(updateResult.Errors);
                    return;
                }

                if (statusComboBox.SelectedIndex != (int)updateResult.Value.Status)
                {
                    var status = (TodoItemStatus)statusComboBox.SelectedIndex;
                    var statusResult = await mediator.Send(
                        new Todo.Application.TodoItems.Commands.ChangeTodoStatus.ChangeTodoStatusCommand(
                            _editId.Value,
                            status));

                    if (statusResult.IsError)
                    {
                        UiResults.ShowErrors(statusResult.Errors);
                        return;
                    }
                }
            }

            DialogResult = DialogResult.OK;
            Close();
        }
        finally
        {
            saveButton.Enabled = true;
            UseWaitCursor = false;
        }
    }
}
