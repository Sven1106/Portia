let form = document.getElementById("root");
form.addEventListener("submit", function (e) {
    e.preventDefault();
})

function createTask(ulElementId) {
    
    let ulElement = document.getElementById(ulElementId);
    let lastLiElement = ulElement.lastChild;
    let liElementId = createLiElementId(lastLiElement, ulElementId);
    let itemUlElementId = createElementId(liElementId, "items");

    let taskNameInput = createInput("text", createElementId(liElementId, "taskName"));
    let deleteButton = createDeleteButton(liElementId);

    let newLiElement = createLiElementWithId(liElementId);

    let htmlElements = [];
    htmlElements.push(taskNameInput);
    htmlElements.push(deleteButton);

    let brLabelButtonElements = createBrLabelButton(itemUlElementId, "items");
    htmlElements = htmlElements.concat(brLabelButtonElements);

    newLiElement.appendChildren(htmlElements);
    ulElement.appendChild(newLiElement);
    console.log(document.getElementById("root"));
}

function createItemInUl(ulElementId) {
    let ulElement = document.getElementById(ulElementId);
    let lastLiElement = ulElement.lastChild;
    let liElementId = createLiElementId(lastLiElement, ulElementId);
    let itemUlElementId = createElementId(liElementId, "attributes");

    let values = ["string", "number", "boolean", "object"]

    let nameInput = createInput("text", createElementId(liElementId, "name"))
    let deleteButton = createDeleteButton(liElementId);

    let select = createSelect(values, createElementId(liElementId, "type"), function (e) {
        let brLabelButtonElements;
        if (this.value == "object") {
            brLabelButtonElements = createBrLabelButton(itemUlElementId, "attributes");
            newLiElement.appendChildren(brLabelButtonElements);
        }
        else if (this.value != "object" && brLabelButtonElements != null) {
            newLiElement.removeChildren(brLabelButtonElements);
            console.log(document.getElementById("root"));
        }
    });
    let getMultipleFromPageInput = createInput("checkbox", createElementId(liElementId, "getMultipleFromPage"))
    let isRequired = createInput("checkbox", createElementId(liElementId, "isRequired"))
    let xpathInput = createInput("text", createElementId(liElementId, "xpath"))

    let htmlElements = [];
    htmlElements.push(nameInput);
    htmlElements.push(deleteButton);
    htmlElements.push(select);
    htmlElements.push(getMultipleFromPageInput);
    htmlElements.push(isRequired);
    htmlElements.push(xpathInput);

    let newLiElement = createLiElementWithId(liElementId);
    newLiElement.appendChildren(htmlElements);
    ulElement.appendChild(newLiElement);
    console.log(document.getElementById("root"));
}
function deleteElementById(id) {
    let element = document.getElementById(id);
    element.parentElement.removeChild(element)
}

function createBrLabelButton(itemUlElementId, name) {
    let htmlElements = [];
    let brElement = document.createElement("br");
    let itemsLabelElement = createLabel(name);
    let itemsButtonElement = createOnclickButton(createItemInUl, itemUlElementId, "+");
    let itemsUlElement = createUlElementWithId(itemUlElementId);
    htmlElements.push(brElement);
    htmlElements.push(itemsLabelElement);
    htmlElements.push(itemsButtonElement);
    htmlElements.push(itemsUlElement);
    return htmlElements;

}

function createDeleteButton(liElementId) {
    let deleteButton = createOnclickButton(deleteElementById, liElementId, "-");
    deleteButton.setAttribute("class", "deleteButton");
    return deleteButton;
}

//CORE FUNCTIONS
function createOnclickButton(callback, id, value) {

    let button = document.createElement("button");
    button.addEventListener("click",function(){
        callback(id)
    })
    button.appendChild(document.createTextNode(value));
    return button;
}

function createSelect(values, id, callBack) {
    let selectElement = document.createElement("select");
    selectElement.setAttribute("id", id);
    for (const value of values) {
        let opt = document.createElement("option");
        opt.text = value;
        selectElement.add(opt);
    }
    selectElement.addEventListener("change", callBack);
    return selectElement;
}

function createLabel(value) {
    let label = document.createElement("label");
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

function createInput(type, id, value = "") {
    let inputField = document.createElement("input");
    inputField.setAttribute("id", id);
    inputField.setAttribute("type", type);
    if (value != "") {
        inputField.setAttribute("value", value);
    }
    return inputField;
}

function createLiElementId(lastLiElement, ulElementId) {
    let newIndex = 0;
    if (lastLiElement != null) {
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