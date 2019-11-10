let form = document.getElementById("root");
form.addEventListener("submit", function (e) {
    e.preventDefault();
})
let createTaskButton = document.getElementById("taskCreateButton");
createTaskButton.addEventListener("click", function () {
    createTask('root.tasks', this)
}
);

function createContainer(classes = "") {
    let container = document.createElement("div");
    if (classes != "") {
        container.setAttribute("class", classes);
    }
    return container;
}

function createTask(ulElementId) {

    let ulElement = document.getElementById(ulElementId);
    let lastLiElement = ulElement.lastElementChild;
    let liElementId = createNewLiElementId(lastLiElement, ulElementId);
    let itemUlElementId = createElementId(liElementId, "items");

    let taskNameContainer = createContainer("control has-icons-left is-expanded"); document.createElement("div");

    let taskNameInput = createInput(
        createElementId(liElementId, "taskName"),
        "text",
        "input is-small");
    taskNameContainer.appendChild(taskNameInput);

    let taskNameIconContainer = createContainer("icon is-small is-left");
    let taskNameIcon = document.createElement("i");
    taskNameIcon.setAttribute("class", "fas fa-cogs");
    taskNameIconContainer.appendChild(taskNameIcon);
    taskNameContainer.appendChild(taskNameIconContainer);


    let deleteButton = createDeleteButton(liElementId);
    let deleteButtonContainer = createContainer("control");
    deleteButtonContainer.appendChild(deleteButton);

    let taskContainer = createContainer("field is-grouped");
    taskContainer.appendChild(taskNameContainer);
    taskContainer.appendChild(deleteButtonContainer);

    let newLiElement = createLiElementWithId(liElementId);
    let htmlElements = [];
    htmlElements.push(taskContainer);

    let ulContainer = document.createElement("div");
    let brLabelButtonElements = createLabelButton(itemUlElementId, "Items");
    ulContainer.appendChildren(brLabelButtonElements);
    htmlElements = htmlElements.concat(ulContainer);

    newLiElement.appendChildren(htmlElements);
    ulElement.appendChild(newLiElement);
    //console.log(document.getElementById("root"));
}

function createItemInUl(ulElementId) {
    let ulElement = document.getElementById(ulElementId);
    let lastLiElement = ulElement.lastElementChild;
    let newLiElementId = createNewLiElementId(lastLiElement, ulElementId);
    let itemUlElementId = createElementId(newLiElementId, "attributes");

    let nameInputId = createElementId(newLiElementId, "name");
    let nameInput = createInput(
        nameInputId,
        "text",
        "input is-small"
    );
    let nameInputContainer = createContainer("control has-icons-left is-expanded");
    nameInputContainer.appendChild(nameInput);
    let nameInputIconContainer = createContainer("icon is-small is-left");
    nameInputContainer.appendChild(nameInputIconContainer);



    let deleteButton = createDeleteButton(newLiElementId);
    let deleteButtonContainer = createContainer("control");
    deleteButtonContainer.appendChild(deleteButton);

    let values = ["string", "number", "boolean", "object"];
    let labelButtonElements;
    let select = createSelect(
        createElementId(newLiElementId, "type"),
        values,
        function () {
            if (this.value == "object") {
                labelButtonElements = createLabelButton(itemUlElementId, "Attributes");
                newLiElement.appendChildren(labelButtonElements);
            }
            else if (this.value != "object") {
                if (labelButtonElements != undefined) {
                    newLiElement.removeChildren(labelButtonElements);
                    labelButtonElements = undefined;
                }
            }
            switch (this.value) {
                case "string":
                    setIconOfInput(nameInputId, "fa-font");
                    break;
                case "number":
                    setIconOfInput(nameInputId, "fa-sort-numeric-down");
                    break;
                case "boolean":
                    setIconOfInput(nameInputId, "fa-check-square");
                    break;
                case "object":
                    setIconOfInput(nameInputId, "fa-cubes");
                    break;
                default:
                    break;
            }
        }, "select is-small"
    );
    let selectContainer = createContainer("control");
    selectContainer.appendChild(select);

    let getMultipleFromPageInput = createInput(
        createElementId(newLiElementId, "getMultipleFromPage"),
        "checkbox",
        "checkbox is-small");
    let getMultipleFromPageInputContainer = createContainer("control");
    let getMultipleFromPageInputLabel = createLabel("Multiple", "", "label is-small");
    getMultipleFromPageInputLabel.prepend(getMultipleFromPageInput);
    getMultipleFromPageInputContainer.appendChild(getMultipleFromPageInputLabel);

    let isRequired = createInput(
        createElementId(newLiElementId, "isRequired"),
        "checkbox",
        "checkbox is-small"
    );
    let isRequiredContainer = createContainer("control");
    let isRequiredLabel = createLabel("Required", "", "label is-small");
    isRequiredLabel.prepend(isRequired);
    isRequiredContainer.appendChild(isRequiredLabel);


    let row1Container = createContainer("field is-grouped");
    row1Container.append(nameInputContainer, deleteButtonContainer);

    let row2Container = createContainer("field is-grouped");
    row2Container.append(selectContainer,
        getMultipleFromPageInputContainer,
        isRequiredContainer);

    let xpathInput = createInput(
        createElementId(newLiElementId, "xpath"),
        "text",
        "input is-small"
    );
    let xpathInputContainer = createContainer("control is-expanded");
    xpathInputContainer.appendChild(xpathInput);
    let row3Container = createContainer("field is-grouped");
    row3Container.append(xpathInputContainer);

    let newLiElement = createLiElementWithId(newLiElementId);
    newLiElement.append(row1Container, row2Container, row3Container);
    ulElement.appendChild(newLiElement);
    setIconOfInput(nameInputId, "fa-font");
}
function setIconOfInput(inputId, icon) {
    let inputElement = document.getElementById(inputId);
    let parentElement = inputElement.parentElement;
    let iconContainer = parentElement.getElementsByClassName("icon")[0];
    iconContainer.innerHTML = "";
    let inputIcon = document.createElement("i");
    inputIcon.setAttribute("class", "fas " + icon);
    iconContainer.append(inputIcon);
}
function deleteElementById(id) {
    let element = document.getElementById(id);
    element.remove();
}

function createLabelButton(itemUlElementId, name) {
    let htmlElements = [];
    let itemsLabelElement = createLabel(name, itemUlElementId, "label");
    let createIconContainer = document.createElement("span");
    createIconContainer.setAttribute("class", "icon");
    let createIcon = document.createElement("i");
    createIcon.setAttribute("class", "fas fa-plus-circle fa-lg");
    createIconContainer.append(createIcon);
    let itemsButtonElement = createOnclickButton(itemUlElementId, createItemInUl, createIcon, "button is-small is-fullwidth is-success");
    itemsButtonElement.appendChild(createIconContainer);
    let itemsUlElement = createUlElementWithId(itemUlElementId);
    htmlElements.push(itemsLabelElement);
    htmlElements.push(itemsUlElement);
    htmlElements.push(itemsButtonElement);
    return htmlElements;

}

function createDeleteButton(liElementId) {
    let deleteIconContainer = document.createElement("div");
    deleteIconContainer.setAttribute("class", "icon is-small");
    let deleteIcon = document.createElement("i");
    deleteIcon.setAttribute("class", "fas fa-trash-alt");
    deleteIconContainer.appendChild(deleteIcon);
    let deleteButton = createOnclickButton(liElementId, deleteElementById, deleteIconContainer, "button is-small is-danger");
    return deleteButton;
}

//CORE FUNCTIONS
function createOnclickButton(id, callback, value, classes = "") {

    let button = document.createElement("button");
    if (classes != "") {
        button.setAttribute("class", classes);
    }
    button.addEventListener("click", function () {
        callback(id)
    })
    button.appendChild(value);
    return button;
}

function createSelect(id, values, callBack, classes = "") {
    let selectElement = document.createElement("select");
    selectElement.setAttribute("id", id);
    if (classes != "") {
        selectElement.setAttribute("class", classes);
    }
    for (const value of values) {
        let opt = document.createElement("option");
        opt.text = value;
        selectElement.add(opt);
    }
    selectElement.addEventListener("change", callBack);
    return selectElement;
}

function createLabel(value, id = "", classes = "") {
    let label = document.createElement("label");
    if (id != "") {
        label.setAttribute("for", id);
    }
    if (classes != "") {
        label.setAttribute("class", classes);
    }
    label.appendChild(document.createTextNode(value));
    return label;
}

Node.prototype.appendChildren = function (htmlElements) {
    htmlElements.forEach(element => {
        this.appendChild(element);
    });
}

Node.prototype.removeChildren = function (htmlElements) {
    htmlElements.forEach(element => {
        this.removeChild(element);
    });
}

function createInput(id, type, classes = "", value = "") {
    let inputField = document.createElement("input");
    inputField.setAttribute("id", id);
    inputField.setAttribute("type", type);
    if (classes != "") {
        inputField.setAttribute("class", classes);
    }
    if (value != "") {
        inputField.setAttribute("value", value);
    }
    return inputField;
}

function createNewLiElementId(lastLiElement, ulElementId) {
    let newIndex = 0;
    if (lastLiElement != null && lastLiElement.getAttribute("id") != null) {
        let highestLiElementIndex = parseInt(lastLiElement.getAttribute("id").split(/[\s.]+/).pop());
        newIndex = highestLiElementIndex + 1;
    }
    let elementId = createElementId(ulElementId, newIndex);
    return elementId;
}
function createElementId(parentElementId, value) {
    let elementId = parentElementId + "." + value;
    return elementId
}

function createUlElementWithId(id) {
    let ulElement = document.createElement("ul");
    ulElement.setAttribute("id", id);
    return ulElement;
}

function createLiElementWithId(id) {
    let newLiElement = document.createElement("li");
    newLiElement.setAttribute("id", id)
    return newLiElement;
}