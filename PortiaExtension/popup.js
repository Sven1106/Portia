let form = document.getElementById("root");
form.addEventListener("submit", function (e) {
    e.preventDefault();
    //console.log(e);
})

function createTask(ulElementId) {
    let ulElement = document.getElementById(ulElementId);
    let lastLiElement = ulElement.lastChild;
    let liElementId = createLiElementId(lastLiElement, ulElementId);
    let itemUlElementId = createElementId(liElementId, "items");

    let taskNameInput = createInput("text", createElementId(liElementId, "taskName"))

    let newLiElement = createLiElement(liElementId);

    let htmlElements = [];
    htmlElements.push(taskNameInput);
    let brLabelButtonElements = createBrLabelButton(itemUlElementId, "items");
    htmlElements = htmlElements.concat(brLabelButtonElements);

    newLiElement.appendChildren(htmlElements);
    ulElement.appendChild(newLiElement);
    console.log(document.getElementById("root"));
}
function createItem(ulElementId) {
    let ulElement = document.getElementById(ulElementId);
    let lastLiElement = ulElement.lastChild;
    let liElementId = createLiElementId(lastLiElement, ulElementId);
    let itemUlElementId = createElementId(liElementId, "attributes");

    let values = ["string", "number", "boolean", "object"]

    let nameInput = createInput("text", createElementId(liElementId, "name"))
    let select = createSelect(values);
    let getMultipleFromPageInput = createInput("checkbox", createElementId(liElementId, "getMultipleFromPage"))
    let isRequired = createInput("checkbox", createElementId(liElementId, "isRequired"))
    let xpathInput = createInput("text", createElementId(liElementId, "xpath"))


    let newLiElement = createLiElement(liElementId);

    let htmlElements = [];
    htmlElements.push(nameInput);
    htmlElements.push(select);
    htmlElements.push(getMultipleFromPageInput);
    htmlElements.push(isRequired);
    htmlElements.push(xpathInput);

    // let brLabelButtonElements = createBrLabelButton(itemUlElementId, "attributes");
    // htmlElements = htmlElements.concat(brLabelButtonElements);

    newLiElement.appendChildren(htmlElements);
    ulElement.appendChild(newLiElement);
    console.log(document.getElementById("root"));
}

function createBrLabelButton(itemUlElementId, name) {
    let htmlElements = [];
    let brElement = document.createElement("br");
    let itemsLabelElement = createLabel(name);
    let itemsButtonElement = createButton(itemUlElementId);
    let itemsUlElement = createUlElement(itemUlElementId);
    htmlElements.push(brElement);
    htmlElements.push(itemsLabelElement);
    htmlElements.push(itemsButtonElement);
    htmlElements.push(itemsUlElement);
    return htmlElements;

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

function createUlElement(id) {
    let ulElement = document.createElement("ul");
    ulElement.setAttribute("id", id);
    return ulElement;
}

function createLiElement(id) {
    let newLiElement = document.createElement("li");
    newLiElement.setAttribute("id", id)
    return newLiElement;
}

Node.prototype.appendChildren = function (htmlElements) {
    htmlElements.forEach(element => {
        this.appendChild(element);
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

function createSelect(values) {
    let selectElement = document.createElement("select");
    for (const value of values) {
        let opt = document.createElement("option");
        opt.text = value;
        selectElement.add(opt);
    }
    return selectElement;
}


function createLabel(value) {
    let label = document.createElement("label");
    label.appendChild(document.createTextNode(value));
    return label;
}
function createButton(id) {
    let button = document.createElement("button");
    button.setAttribute("onclick", "createItem('" + id + "')");
    button.appendChild(document.createTextNode("+"));
    return button;
}
