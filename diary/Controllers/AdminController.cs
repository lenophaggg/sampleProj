﻿using diary.Models;
using diary.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;

namespace diary.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ILogger<AdminController> _logger;
        private readonly ApplicationDbContext _applicationDbContext;
        private readonly DiaryDbContext _diaryDbContext;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AdminController(ILogger<AdminController> logger,
            ApplicationDbContext applicationDbContext,
            DiaryDbContext diaryDbContext,
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _logger = logger;
            _applicationDbContext = applicationDbContext;
            _diaryDbContext = diaryDbContext;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound("User not found");
            }

            var personContactUser = _diaryDbContext.PersonContactUsers
                                            .FirstOrDefault(pcu => pcu.UserId == user.Id);
            if (personContactUser == null)
            {
                return View();
            }

            var personContact = _applicationDbContext.PersonContacts
                .FirstOrDefault(pc => pc.IdContact == personContactUser.PersonContactId);

            return View(personContact);
        }

        public async Task<IActionResult> TeacherDetails(int id)
        {
            if (id == 0)
            {
                return RedirectToAction("Index", "Teacher"); // Перенаправление на главную страницу 
            }
            var curUser = await _userManager.GetUserAsync(User);
            if (curUser == null)
            {
                return Unauthorized();
            }

            var teacher = await _applicationDbContext.PersonContacts
               .AsNoTracking()
               .FirstOrDefaultAsync(t => t.IdContact == id);

            if (teacher == null)
            {
                return NotFound();
            }

            
            var personContactUser = await _diaryDbContext.PersonContactUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(pcu => pcu.PersonContactId == id);

            IdentityUser user = null;
            List<string> userRoles = new List<string>();

            if (personContactUser != null)
            {
                user = await _userManager.FindByIdAsync(personContactUser.UserId);
                if (user != null)
                {
                    userRoles = (await _userManager.GetRolesAsync(user)).ToList();
                }
            }

            var model = new
            {
                teacher.IdContact,
                teacher.UniversityIdContact,
                teacher.NameContact,
                teacher.Position,
                teacher.AcademicDegree,
                teacher.TeachingExperience,
                teacher.Telephone,
                teacher.Email,
                teacher.Information,
                teacher.ImgPath,
                UserId = user?.Id,
                UserName = user?.UserName,
                UserRoles = userRoles,
                Roles = await _roleManager.Roles.ToListAsync(),
                Classes = await _diaryDbContext.ClassGroupAssignments.Include(cga => cga.Class)                
                                  .Where(c => c.Class.InstructorId == teacher.IdContact)
                                  .ToListAsync() 
            };
            return View(model);
        }

        public async Task<IActionResult> ListTeacher()
        {
            var teachers = await _applicationDbContext.PersonContacts.ToListAsync();
            return View(teachers);
        }

        [HttpGet]
        public async Task<IActionResult> FilterTeachers(string searchTerm)
        {
            var query = _applicationDbContext.PersonContacts.AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(t => t.NameContact.ToLower().Contains(searchTerm.ToLower()));
            }

            var filteredTeachers = await query.ToListAsync();

            return PartialView("_TeachersTable", filteredTeachers);
        }
        #region ManageUsers

        [HttpPost]
        public async Task<IActionResult> CreateUser(int personContactId, string userName, string password, string[] userRoles, string contactType)
        {
            var curUser = await _userManager.GetUserAsync(User);
            if (curUser == null)
            {
                return Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(userName))
            {
                return Json(new { success = false, message = "User name cannot be empty" });
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                return Json(new { success = false, message = "Password cannot be empty" });
            }

            if (userRoles == null || userRoles.Length == 0)
            {
                return Json(new { success = false, message = "At least one role must be selected" });
            }

            if (string.IsNullOrWhiteSpace(contactType))
            {
                return Json(new { success = false, message = "Contact type cannot be empty" });
            }

            object personContact = null;

            if (contactType == "Teacher")
            {
                personContact = await _applicationDbContext.PersonContacts.FirstOrDefaultAsync(pc => pc.IdContact == personContactId);
            }
            else if (contactType == "GroupHead")
            {
                personContact = await _diaryDbContext.GroupHeads.Include(gh => gh.Student).FirstOrDefaultAsync(gh => gh.Student.StudentId == personContactId);
            }

            if (personContact == null)
            {
                return Json(new { success = false, message = $"{contactType} not found" });
            }


            var user = new IdentityUser { UserName = userName.Trim() };
            var result = await _userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                if (contactType == "Teacher")
                {
                    var personContactUser = new PersonContactUserData
                    {
                        UserId = user.Id,
                        PersonContactId = personContactId
                    };
                    _diaryDbContext.PersonContactUsers.Add(personContactUser);
                }
                else if (contactType == "GroupHead")
                {
                    var groupHead = _diaryDbContext.GroupHeads.FirstOrDefault(gh => gh.StudentId == personContactId);
                    groupHead.UserId = user.Id;
                    _diaryDbContext.GroupHeads.Update(groupHead);
                }

                await _diaryDbContext.SaveChangesAsync();

                foreach (var role in userRoles)
                {
                    if (!await _roleManager.RoleExistsAsync(role))
                    {
                        await _roleManager.CreateAsync(new IdentityRole(role));
                    }
                    await _userManager.AddToRoleAsync(user, role);
                }

                return Json(new { success = true });
            }
            else
            {
                return Json(new { success = false, message = string.Join(", ", result.Errors.Select(e => e.Description)) });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteUser(int personContactId, string contactType)
        {
            // Получаем текущего пользователя
            var curUser = await _userManager.GetUserAsync(User);
            if (curUser == null)
            {
                return Unauthorized();
            }

            object personContactUser = null;
            string userId = null;

            // Определяем, с каким типом пользователя работаем (Teacher или GroupHead)
            if (contactType == "Teacher")
            {
                var teacherContact = await _diaryDbContext.PersonContactUsers
                    .FirstOrDefaultAsync(pcu => pcu.PersonContactId == personContactId);
                if (teacherContact != null)
                {
                    personContactUser = teacherContact;
                    userId = teacherContact.UserId;
                }
            }
            else if (contactType == "GroupHead")
            {
                var groupHeadContact = await _diaryDbContext.GroupHeads
                    .FirstOrDefaultAsync(gh => gh.StudentId == personContactId);
                if (groupHeadContact != null)
                {
                    personContactUser = groupHeadContact;
                    userId = groupHeadContact.UserId;
                }
            }

            // Если связь с пользователем не найдена
            if (personContactUser == null)
            {
                return Json(new { success = false, message = "User assignment not found" });
            }

            // Если UserId существует, удаляем связанного пользователя в системе Identity
            if (!string.IsNullOrEmpty(userId))
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user != null)
                {
                    var result = await _userManager.DeleteAsync(user);
                    if (!result.Succeeded)
                    {
                        return Json(new { success = false, message = "Failed to delete the user" });
                    }
                }
            }

            if (contactType == "Teacher")
            {
                _diaryDbContext.PersonContactUsers.Remove((PersonContactUserData)personContactUser);
            }
            else if (contactType == "GroupHead")
            {
                // Установка поля UserId в null, вместо удаления записи
                var groupHead = (GroupHeadData)personContactUser;
                groupHead.UserId = null;
                _diaryDbContext.GroupHeads.Update(groupHead);
            }
            
            await _diaryDbContext.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> GetUserRoles(string userId)
        {

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return Json(new { success = false, message = "User not found" });
            }

            var roles = await _userManager.GetRolesAsync(user);
            return Json(roles);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateUserRoles(string userId, List<string> roles)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = false, message = "User ID is required" });
            }

            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                return Json(new { success = false, message = "User not found" });
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            var removeRolesResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeRolesResult.Succeeded)
            {
                return Json(new { success = false, message = "Failed to remove existing roles" });
            }

            var addRolesResult = await _userManager.AddToRolesAsync(user, roles);
            if (!addRolesResult.Succeeded)
            {
                return Json(new { success = false, message = "Failed to add new roles" });
            }

            return Json(new { success = true });
        }

        // Вспомогательный метод для проверки роли
        public async Task<bool> IsUserInRole(string userId, string roleName)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                return await _userManager.IsInRoleAsync(user, roleName);
            }
            return false;
        }
        #endregion
        #region ManageGroup
        public IActionResult ListGroup()
        {
            var groups = _applicationDbContext.Groups.ToList();

            var facultyGroups = groups
                .Where(g => g.FacultyName != null) 
                .GroupBy(g => g.FacultyName)
                .ToDictionary(g => g.Key, g => g.Select(group => group.Number).ToArray());

            return View(facultyGroups);
        }

        public async Task<IActionResult> GroupDetails(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return RedirectToAction("Index", "Admin"); // Перенаправление на главную страницу
            }

            string group = id;

            // Получаем студентов группы
            var students = await _diaryDbContext.Students
                .Where(s => s.GroupNumber == group)
                .ToListAsync();

            // Получаем старосту группы
            var grouphead = await _diaryDbContext.GroupHeads
                .Include(gh => gh.Student)
                .Where(gh => gh.Student.GroupNumber == group)
                .Select(gh => gh.Student)
                .FirstOrDefaultAsync();

            var groupDetailsViewModel = new GroupDetailsViewModel
            {
                GroupNumber = group,
                Students = students,
                GroupHead = grouphead
            };

            return View(groupDetailsViewModel);
        }
        #endregion

        #region GroupHead
        public async Task<IActionResult> GroupHeadDetails(int id)
        {
            if (id == 0)
            {
                return RedirectToAction("Index", "Admin"); // Перенаправление на главную страницу 
            }

            var studentId = id;

            var groupHead = await _diaryDbContext.GroupHeads
                .Include(gh => gh.Student)
                .FirstOrDefaultAsync(gh => gh.StudentId == studentId);

            if (groupHead == null)
            {
                return NotFound("Group head not found");
            }

            var user = await _userManager.FindByIdAsync(groupHead.UserId);
            var userRoles = user != null ? await _userManager.GetRolesAsync(user) : new List<string>();

            var model = new
            {
                groupHead.Student.StudentId,
                groupHead.Student.Name,
                groupHead.Student.UniversityStudentId,
                groupHead.Student.GroupNumber,
                groupHead.UserId,
                UserName = user?.UserName,
                UserRoles = userRoles,
                Roles = await _roleManager.Roles.ToListAsync()
            };

            return View(model);
        }


        [HttpPost]
        public async Task<IActionResult> AssignGroupHead(int studentId)
        {
            var student = await _diaryDbContext.Students.FindAsync(studentId);
            if (student == null)
            {
                return Json(new { success = false, message = "Student not found" });
            }

            // Удаление текущего старосты
            var currentGroupHead = await _diaryDbContext.GroupHeads
                .Include(gh => gh.Student)
                .FirstOrDefaultAsync(gh => gh.Student.GroupNumber == student.GroupNumber);

            if (currentGroupHead != null)
            {
                var userId = currentGroupHead.UserId;
                _diaryDbContext.GroupHeads.Remove(currentGroupHead);

                if (!string.IsNullOrEmpty(userId))
                {
                    var user = await _userManager.FindByIdAsync(userId);
                    if (user != null)
                    {
                        await _userManager.DeleteAsync(user);

                    }
                }
            }

            // Назначение нового старосты группы
            var newGroupHead = new GroupHeadData
            {
                StudentId = studentId,
                UserId = null // Пользователь на данном этапе не назначается
            };

            _diaryDbContext.GroupHeads.Add(newGroupHead);
            await _diaryDbContext.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> RemoveGroupHead(int studentId)
        {
            var groupHead = await _diaryDbContext.GroupHeads.FirstOrDefaultAsync(gh => gh.StudentId == studentId);
            if (groupHead == null)
            {
                return Json(new { success = false, message = "Group head not found" });
            }

            var userId = groupHead.UserId;
            _diaryDbContext.GroupHeads.Remove(groupHead);

            if (!string.IsNullOrEmpty(userId))
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user != null)
                {
                    await _userManager.DeleteAsync(user);
                }
            }

            await _diaryDbContext.SaveChangesAsync();
            return Json(new { success = true });
        }

        // Метод для добавления студента в группу
        [HttpPost]
        public async Task<IActionResult> AddStudent(string studentName, string universityStudentId, string groupNumber)
        {
            var curUser = await _userManager.GetUserAsync(User);
            if (curUser == null)
            {
                return Unauthorized();
            }

            var student = await _diaryDbContext.Students.FirstOrDefaultAsync(s => s.UniversityStudentId == universityStudentId);
            if (student != null)
            {
                return BadRequest("Студент уже числится в системе");
            }

            var newStudent = new StudentData
            {
                Name = studentName.Trim(),
                UniversityStudentId = universityStudentId.Trim(),
                GroupNumber = groupNumber
            };

            _diaryDbContext.Students.Add(newStudent);
            await _diaryDbContext.SaveChangesAsync();

            return Json(new { success = true });
        }

        // Метод для удаления студента из группы
        [HttpPost]
        public async Task<IActionResult> RemoveStudent(int studentId)
        {
            var curUser = await _userManager.GetUserAsync(User);
            if (curUser == null)
            {
                return Unauthorized();
            }

            var student = await _diaryDbContext.Students.FindAsync(studentId);
            if (student == null)
            {
                return NotFound("Student not found");
            }

            var groupHead = await _diaryDbContext.GroupHeads
                .FirstOrDefaultAsync(gh => gh.StudentId == studentId);
            if (groupHead != null)
            {
                return BadRequest("Cannot remove student who is a group head");
            }

            _diaryDbContext.Students.Remove(student);
            await _diaryDbContext.SaveChangesAsync();

            return Json(new { success = true });
        }
        #endregion

        #region StudentAbsence

        public async Task<IActionResult> StudentAbsences(int? requestId = null)
        {
            return RedirectToAction("StudentAbsences", "Shared");
        }

        public async Task<IActionResult> CreateStudentAbsenceRequest()
        {
            return RedirectToAction("CreateStudentAbsenceRequest", "Shared");
        }

        public async Task<IActionResult> StudentAbsencesDetails(int requestId)
        {
           
            return View("~/Views/Shared/StudentAbsencesDetails.cshtml");  // Убедитесь, что указанный путь правильный
        }

        //[HttpPost]
        //public async Task<IActionResult> CreateStudentAbsenceRequest(StudentAbsenceViewModel model)
        //{
        //    var user = await _userManager.GetUserAsync(User);
        //    if (user == null)
        //    {
        //        return Unauthorized("User not found");
        //    }

        //    var groupHead = await _diaryDbContext.GroupHeads
        //        .Include(gh => gh.Student)
        //        .FirstOrDefaultAsync(gh => gh.UserId == user.Id);

        //    if (groupHead == null)
        //    {
        //        return NotFound("Group head not found");
        //    }

        //    var newRequest = new StudentAbsencesData
        //    {
        //        StudentId = groupHead.Student.StudentId,
        //        GroupNumber = groupHead.Student.GroupNumber,
        //        Reason = model.Reason,
        //        StartDate = model.StartDate,
        //        EndDate = model.EndDate,
        //        Status = AbsencesStatus.Submitted // Заявка создается сразу с этим статусом
        //    };

          
        //    _diaryDbContext.StudentAbsences.Add(newRequest);
        //    await _diaryDbContext.SaveChangesAsync();

        //    return RedirectToAction("StudentAbsences");
        //}

        #endregion
    }
}