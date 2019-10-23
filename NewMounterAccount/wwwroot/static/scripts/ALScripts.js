$(function () {
    $("#accordion").accordion({
        collapsible: true,
        heightStyle: "content",
        active: false
    });

    $("#supportTable").accordion({
        header: "> div > h3",
        collapsible: true,
        heightStyle: "content",
        active: false
    });

    $("#remarksAccordion").accordion({
        collapsible: true,
        heightStyle: "content",
        active: false
    });
});

function PowerLineId(id) {
    $('#PowerLineSupportId').val(id);
}

function KdeId(id) {
    $('#KDEId').val(id);
}

function CloseModalPU() {
    $('#addPU').modal('toggle');
}

function PuTableId(id) {
    var addPuForm = document.getElementById('addPUForm');
    addPuForm.setAttribute('data-ajax-update', '#puTable_' + id);
}


$(document).ready(function () {
    $('#sim').hide();
    $("#PhoneNumber").prop('required', false);
    $("#PhoneNumber").val('');
    var max_fields = 9; //maximum input boxes allowed
    var wrapper = $("#brigadeRow"); //Fields wrapper
    var add_button = $("#add_field_button"); //Add button ID
    var remove_button = $("#remove_field"); //Remove button
    var submit_button = $("#submitButton");
    var brigade_field;
    var x = 3; //initlal text box count

    function addField() {
        if (x < max_fields) { //max input box allowed
        $(wrapper).append('<div class="col-md-3 mb-3" id="BrigadeElement' + x + '"> <label for= "Brigade' + x + '"> Работник ' + x + '</label>  <input type="text" class="form-control" form="form1" id="Brigade' + x + '" name="Brigade' + x + '" required pattern="[А-Яа-я]+ [А-Яа-я]{1}\.[А-Яа-я]{1}\." /></div>'); //add input box
        x++; //text box increment
    }
}

    $(add_button).click(function (e) { //on add input button click
        e.preventDefault();
        addField();
    });

    $(remove_button).click(function (e) {
        if (x > 3) {
            e.preventDefault();
            $("#BrigadeElement" + (x - 1)).remove();
            x--;
        }
    });

    $(submit_button).click(function (e) {
        brigade_field = $("#Brigade1").val() + "$";
        for (i = 2; i < x; i++) {
            brigade_field += $("#Brigade" + i).val() + "$";
        }
        brigade_field = brigade_field.substring(0, brigade_field.length - 1);
        $("#Brigade").val(brigade_field);
    });

});
    
function val() {
    var val1 = document.getElementById("substationType").value;
    var val2 = document.getElementById("SubstationNumber").value;
    document.getElementById("Substation").value = val1 + " " + val2;
}
    
$('#date').datepicker({
    startDate: new Date(),
    maxDate: new Date()
})



$(document).ready(function () {
    if ("@Model.Id" === "0") {
        var date = new Date();
        var day = date.getDate();
        var month = date.getMonth() + 1;
        if (day.toString().length === 1)
            day = "0" + day;
        if (month.toString().length === 1)
            month = "0" + month;
        $('#date').val(day + '.' + month + '.' + date.getFullYear());
    }
    else {
        $('#date').val("@Html.Raw(Model.Date.ToShortDateString())");
    }
});

function OpenEditPowerLineSupportEditor(number, type, fixators, id) {
    $('#SupportNumberEdit').val(number);
    $('#PowerLineTypeEdit').val(type);
    $('#FixatorsCountEdit').val(fixators);
    $('#PowerLineSupportIdEdit').val(id);
    $('#editSupport').modal('toggle');
}

function EditPowerLineSupport() {
    var xhr = new XMLHttpRequest();
    var body = "supportNumber=" + encodeURIComponent($('#SupportNumberEdit').val()) + "&powerLineType=" + encodeURIComponent($('#PowerLineTypeEdit').val())
                + "&fixatorsCount=" + encodeURIComponent($('#FixatorsCountEdit').val()) + "&id=" + encodeURIComponent($('#PowerLineSupportIdEdit').val());
    xhr.onreadystatechange = function () {
        if (xhr.readyState === 4 && xhr.status === 200) {
            $('#editSupport').modal('toggle');
            $('#supportTable').replaceWith(xhr.response);
            $("#supportTable").accordion({
                header: "> div > h3",
                collapsible: true,
                heightStyle: "content",
                active: false
            });
        }
    }
    xhr.open("POST", '/Report/EditSupport', true);
    xhr.setRequestHeader('Content-Type', 'application/x-www-form-urlencoded');
    xhr.send(body);
}
//ОТКРЫТИЕ АКТА
$('#WorkPermit').val("@Html.Raw(Model.WorkPermit)");
$('#substationType').val("@Html.Raw(Model.Substation.ToString().Substring(0, 2))");
$('#SubstationNumber').val("@Html.Raw(Model.Substation.ToString().Substring(3))");
$('#Fider').val("@Html.Raw(Model.Fider)");
$('#Local').val("@Html.Raw(Model.Local)");
var brigade = "@Html.Raw(Model.Brigade)".split('$');
for (i = 0; i < brigade.length; i++) {
    if (i >1)
        addField(i + 1);
    $("#Brigade" + (i + 1)).val(brigade[i]);
}

function addField(x) {
     $('#brigadeRow').append('<div class="col-md-3 mb-3" id="BrigadeElement' + x + '"> <label for= "Brigade' + x + '"> Работник ' + x + '</label>  <input type="text" class="form-control" form="form1" id="Brigade' + x + '" name="Brigade' + x + '" required pattern="[А-Яа-я]+ [А-Яа-я]{1}\.[А-Яа-я]{1}\." /></div>'); //add input box
}

$('#addPLSbtn').click(function (e) { //Добавление опоры
    e.preventDefault();
    $("#supportAddForm").validate();
    if ($("#supportAddForm").valid()) {
        var xhr = new XMLHttpRequest();
        var body = "supportNumber=" + encodeURIComponent($('#SupportNumber').val()) + "&powerLineType=" + encodeURIComponent($('#PowerLineType').val())
                + "&fixatorsCount=" + encodeURIComponent($('#FixatorsCount').val()) + "&reportId=" + encodeURIComponent(@Model.Id);
    xhr.onreadystatechange = function () {
        if (xhr.readyState === 4 && xhr.status === 200) {
        $('#supportTable').replaceWith(xhr.response);

        $("#supportTable").accordion({
            header: "> div > h3",
            collapsible: true,
            heightStyle: "content",
            active: false
            });
        }
    }
}

    xhr.open("POST", '/Report/AddSupport', true);
    xhr.setRequestHeader('Content-Type', 'application/x-www-form-urlencoded');
    xhr.send(body);
}
});

        function DeletePowerLineSupport(id) {
            var xhr = new XMLHttpRequest();
            var body = "supportNumber=" + encodeURIComponent(id);
                xhr.onreadystatechange = function () {
                    if (xhr.readyState === 4 && xhr.status === 200) {
                $('#supportTable').replaceWith(xhr.response);

            $("#supportTable").accordion({
                header: "> div > h3",
            collapsible: true,
            heightStyle: "content",
            active: false
        });
    }
}
xhr.open("POST", '/Report/DeleteSupport', true);
xhr.setRequestHeader('Content-Type', 'application/x-www-form-urlencoded');
xhr.send(body);
}

        $('#kdeAddBtn').click(function (e) { //Добавление КДЕ на опору
                e.preventDefault();
            var xhr = new XMLHttpRequest();
            var body = "supportNumber=" + encodeURIComponent($('#PowerLineSupportId').val()) + "&kdeType=" + encodeURIComponent($('#KDEType').val());
        xhr.onreadystatechange = function () {
            if (xhr.readyState === 4 && xhr.status === 200) {
                $('#addKDE').modal('toggle');
            $('#Support_' + $('#PowerLineSupportId').val()).append(xhr.response);
        }
    }
    xhr.open("POST", '/Report/AddKde', true);
    xhr.setRequestHeader('Content-Type', 'application/x-www-form-urlencoded');
    xhr.send(body);
});

        function DeleteKde(id) {
        var xhr = new XMLHttpRequest();
            var body = "kdeId=" + encodeURIComponent(id);
        xhr.onreadystatechange = function () {
            if (xhr.readyState === 4 && xhr.status === 200) {
                if (xhr.responseText.length > 0) {
                alert(xhr.responseText);
            }
                else {
                    var kde = $('#kde_' + id);
            kde.remove();
        }

    }
}
xhr.open("POST", '/Report/DeleteKde', true);
xhr.setRequestHeader('Content-Type', 'application/x-www-form-urlencoded');
xhr.send(body);
}

        function DeletePu(id, kdeId) {
        var xhr = new XMLHttpRequest();
            var body = "id=" + encodeURIComponent(id);
        xhr.onreadystatechange = function () {
            if (xhr.readyState === 4 && xhr.status === 200) {
                $('#puTable_' + kdeId).html(xhr.response);
            }
        }
        xhr.open("POST", '/Report/DeleteALPU', true);
        xhr.setRequestHeader('Content-Type', 'application/x-www-form-urlencoded');
        xhr.send(body);
    }

        function CheckDevice() {//Проверка возможности добавить ПУ в отчет
            var serial = document.getElementById("Serial").value.toString();
            var xhr = new XMLHttpRequest();
            var body = "serialNumber=" + encodeURIComponent(serial) + "&contractId=" + encodeURIComponent(@ViewBag.Contract.Id);
            xhr.onreadystatechange = function () {
            if (xhr.readyState === 4 && xhr.status === 200 && xhr.responseText.length > 0) {
                $('#Serial').val('');
            $('#sim').hide();
            $("#PhoneNumber").prop('required', false);
            $("#PhoneNumber").val('');
            alert(xhr.responseText);
        }
            else {
                if ($('#Serial').val().startsWith("011889") || $('#Serial').val().startsWith("012095") || $('#Serial').val().startsWith("012099")) {
                $('#sim').show();
            $("#PhoneNumber").prop('required', true);
        }
                else {
                $('#sim').hide();
            $("#PhoneNumber").prop('required', false);
            $("#PhoneNumber").val('');
        }
    }
}

    xhr.open("POST", '/Report/CheckDevice', true);
    xhr.setRequestHeader('Content-Type', 'application/x-www-form-urlencoded');
    xhr.send(body);
}

        function CheckKDE(id) {//Проверка возможности добавить ПУ в КДЕ

        var xhr = new XMLHttpRequest();
            var body = "kdeId=" + encodeURIComponent(id);
        xhr.onreadystatechange = function () {
            if (xhr.readyState === 4 && xhr.status === 200) {
                if (xhr.responseText.length > 0)
                alert(xhr.responseText);
            else
                $('#addPU').modal('toggle');
        }
    }

    xhr.open("POST", '/Report/CheckKDE', true);
    xhr.setRequestHeader('Content-Type', 'application/x-www-form-urlencoded');
    xhr.send(body);
}

        function SendReport(reportId) {
                $('#sendToCurator').modal('toggle');
            var xhr = new XMLHttpRequest();
            var body = "id=" + encodeURIComponent(reportId) + "&curatorId=" + encodeURIComponent($('#UserId').val());
        xhr.onreadystatechange = function () {
            if (xhr.readyState === 4 && xhr.status === 200) {
                alert(xhr.responseText);
            }
        }

        xhr.open("POST", '/Report/SendAlReport', true);
        xhr.setRequestHeader('Content-Type', 'application/x-www-form-urlencoded');
        xhr.send(body);
        }

        function AddAdditionalMaterialOpenModal(deviceItemId, kdeId) {
                $('#addMaterial').modal("toggle");
            $('#AdditionalMaterialDeviceId').val(deviceItemId);
            $('#AdditionalMaterialKDEId').val(kdeId);

        }

        function AddAdditionalMaterial() {
            var xhr = new XMLHttpRequest();
            var materialId = $('#MaterialId').val();
            var volume = $('#MaterialVolume').val();
            var kdeId = $('#AdditionalMaterialKDEId').val();
            var body = "deviceItemId=" + encodeURIComponent($('#AdditionalMaterialDeviceId').val()) + "&materialId=" + encodeURIComponent(materialId) + "&volume=" + encodeURIComponent(volume);
            xhr.onreadystatechange = function () {
                if (xhr.readyState === 4 && xhr.status === 200) {
                $('#addMaterial').modal("toggle");
            $('#kde_' + kdeId.toString()).html(xhr.response);
        }
    }
    xhr.open("POST", '/Report/AddAdditionalMaterial', true);
    xhr.setRequestHeader('Content-Type', 'application/x-www-form-urlencoded');
    xhr.send(body);
}

        function DeleteAdditionalMaterial(materialId, KDEId) {
            var xhr = new XMLHttpRequest();
            var body = "id=" + encodeURIComponent(materialId) + "&kdeId=" + encodeURIComponent(KDEId);
            xhr.onreadystatechange = function () {
                if (xhr.readyState === 4 && xhr.status === 200) {
                $('#kde_' + KDEId).html(xhr.response);
            }
        }
        xhr.open("POST", '/Report/DeleteAdditionalMaterial', true);
        xhr.setRequestHeader('Content-Type', 'application/x-www-form-urlencoded');
        xhr.send(body);
    }

    </script>
}