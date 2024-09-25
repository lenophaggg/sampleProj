// Скрипт для отображения загрузки до загрузки всех строк со статусами 
document.addEventListener("DOMContentLoaded", function () {
    var container = document.querySelector(".container-fluid");
    var statusElements = document.querySelectorAll("td > span");  // Находим все <span> со статусами
    var totalStatuses = statusElements.length;
    var loadedStatuses = 0;

    // Функция проверки загрузки статусов
    function checkAllStatusesLoaded() {
        loadedStatuses++;
        if (loadedStatuses === totalStatuses) {
            // Скрываем индикатор загрузки и показываем основной контент
            document.getElementById("loader").style.display = "none";
            container.style.display = "block";
        }
    }

    // Проверяем, если все строки со статусами прогрузились
    statusElements.forEach(function (status) {
        
        if (status.innerHTML.trim() !== "") {
            checkAllStatusesLoaded();
        } else {            
            var observer = new MutationObserver(function (mutations) {
                mutations.forEach(function (mutation) {
                    if (mutation.type === 'childList' && mutation.addedNodes.length > 0) {
                        checkAllStatusesLoaded();
                        observer.disconnect();  
                    }
                });
            });

            observer.observe(status, { childList: true });
        }
    });

    // Если нет строк со статусами, сразу показываем контент
    if (totalStatuses === 0) {
        document.getElementById("loader").style.display = "none";
        container.style.display = "block";
    }
});


function createStudentAbsenceRequestForm() {
    const form = document.getElementById('createStudentAbsenceRequestForm');
    const formData = new FormData(form);

    $.ajax({
        url: form.action,
        type: 'POST',
        data: formData,
        processData: false,
        contentType: false,
        success: function(response) {
            if (response.success) {
                location.reload();
            } else {
                alert(response.message);
            }
        },
        error: function() {
            alert('An error occurred while creating the student absence request.');
        }
    });
}