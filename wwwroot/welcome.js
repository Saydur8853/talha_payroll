const title = document.getElementById("welcomeTitle");
const userName = document.getElementById("userName");
const dashboard = document.querySelector(".dashboard");
const menuToggle = document.getElementById("menuToggle");
const sideOverlay = document.getElementById("sideOverlay");
const totalEmployees = document.getElementById("totalEmployees");
const activeEmployees = document.getElementById("activeEmployees");
const inactiveEmployees = document.getElementById("inactiveEmployees");
const newJoinersLabel = document.getElementById("newJoinersLabel");
const newJoinersValue = document.getElementById("newJoinersValue");
const closeEmployees = document.getElementById("closeEmployees");
const releaseEmployees = document.getElementById("releaseEmployees");
const resignEmployees = document.getElementById("resignEmployees");
const totalWorker = document.getElementById("totalWorker");
const totalStaff = document.getElementById("totalStaff");
const totalOfficer = document.getElementById("totalOfficer");
const totalMale = document.getElementById("totalMale");
const totalFemale = document.getElementById("totalFemale");
const cashPay = document.getElementById("cashPay");
const bankPay = document.getElementById("bankPay");
const mobilePay = document.getElementById("mobilePay");
const taxHolder = document.getElementById("taxHolder");
const quarterHolder = document.getElementById("quarterHolder");
const incrementLabel = document.getElementById("incrementLabel");
const incrementValue = document.getElementById("incrementValue");
const offDuty = document.getElementById("offDuty");
const leaveLabel = document.getElementById("leaveLabel");
const leaveValue = document.getElementById("leaveValue");
const maternityCount = document.getElementById("maternityCount");
const releaseLabel = document.getElementById("releaseLabel");
const resignLabel = document.getElementById("resignLabel");
const sectionLabel = document.getElementById("sectionLabel");
const menuItems = Array.from(document.querySelectorAll(".menu-item[data-view]"));
const views = Array.from(document.querySelectorAll(".view[data-view]"));
const employeeForm = document.getElementById("employeeForm");
const employeeDelete = document.getElementById("employeeDelete");
const employeeReset = document.getElementById("employeeReset");
const employeeNotice = document.getElementById("employeeNotice");
const employeeRowId = document.getElementById("employeeRowId");
const lengthOfService = document.getElementById("lengthOfService");
const leaveBalance = document.getElementById("leaveBalance");
const employeePhotoPreview = document.getElementById("employeePhotoPreview");
const employeeSignaturePreview = document.getElementById("employeeSignaturePreview");
const employeePhotoInput = document.getElementById("employeePhotoInput");
const employeeSignatureInput = document.getElementById("employeeSignatureInput");
const employeePhotoUpload = document.getElementById("employeePhotoUpload");
const employeeSignatureUpload = document.getElementById("employeeSignatureUpload");
const logoutLink = document.getElementById("logoutLink");

const EMPLOYEE_STORAGE_KEY = "visorhr.employees";
const ACTIVE_VIEW_KEY = "visorhr.activeView";
const AUTH_STORAGE_KEY = "visorhr.auth";

if (!localStorage.getItem(AUTH_STORAGE_KEY)) {
  window.location.href = "/";
}

const params = new URLSearchParams(window.location.search);
const unit = params.get("unit") || localStorage.getItem("visorhr.unit");
const user = params.get("user");

if (unit) {
  title.textContent = `Welcome to ${unit}`;
} else {
  title.textContent = "Welcome";
}

if (userName) {
  userName.textContent = user || "User";
}

const defaultWelcomeTitle = title ? title.textContent : "Welcome";

const closeMenu = () => {
  if (!dashboard) {
    return;
  }
  dashboard.classList.remove("is-open");
  if (menuToggle) {
    menuToggle.setAttribute("aria-expanded", "false");
  }
};

if (menuToggle && dashboard) {
  menuToggle.addEventListener("click", () => {
    const isOpen = dashboard.classList.toggle("is-open");
    menuToggle.setAttribute("aria-expanded", String(isOpen));
  });
}

if (sideOverlay) {
  sideOverlay.addEventListener("click", closeMenu);
}

window.addEventListener("resize", () => {
  if (window.innerWidth > 720) {
    closeMenu();
  }
});

const updateHeaderForView = (viewId) => {
  if (!title) {
    return;
  }
  if (viewId === "employees") {
    title.textContent = "Employees";
    if (sectionLabel) {
      sectionLabel.textContent = "HR";
    }
    return;
  }
  title.textContent = defaultWelcomeTitle;
  if (sectionLabel) {
    sectionLabel.textContent = "Unit";
  }
};

const setView = (viewId) => {
  if (!viewId || views.length === 0) {
    return;
  }
  views.forEach((view) => {
    view.classList.toggle("is-active", view.getAttribute("data-view") === viewId);
  });
  menuItems.forEach((item) => {
    item.classList.toggle("is-active", item.getAttribute("data-view") === viewId);
  });
  updateHeaderForView(viewId);
  localStorage.setItem(ACTIVE_VIEW_KEY, viewId);
};

if (menuItems.length) {
  menuItems.forEach((item) => {
    item.addEventListener("click", (event) => {
      event.preventDefault();
      const viewId = item.getAttribute("data-view");
      setView(viewId);
      closeMenu();
    });
  });
  const storedView = localStorage.getItem(ACTIVE_VIEW_KEY);
  const defaultView = menuItems.find((item) => item.classList.contains("is-active"))?.getAttribute("data-view");
  const validStored = storedView && views.some((view) => view.getAttribute("data-view") === storedView);
  setView((validStored ? storedView : defaultView) || menuItems[0].getAttribute("data-view"));
}

const getStoredEmployees = () => {
  const stored = localStorage.getItem(EMPLOYEE_STORAGE_KEY);
  if (!stored) {
    return [];
  }
  try {
    const parsed = JSON.parse(stored);
    return Array.isArray(parsed) ? parsed : [];
  } catch (error) {
    return [];
  }
};

const saveEmployees = (employees) => {
  localStorage.setItem(EMPLOYEE_STORAGE_KEY, JSON.stringify(employees));
};

const normalizeCode = (value) => value.trim().toUpperCase();

const toBijoy = (value) => {
  if (!value) {
    return "";
  }
  if (window.Unicode2ASCII && typeof window.Unicode2ASCII.ConvertToASCII === "function") {
    try {
      return window.Unicode2ASCII.ConvertToASCII("bijoy", value);
    } catch (error) {
      return value;
    }
  }
  return value;
};

const TEXT_FIELDS = [
  "empCode",
  "erpCode",
  "empName",
  "empNameBang",
  "fatherName",
  "fatherNameBang",
  "motherName",
  "motherNameBang",
  "spouseName",
  "spouseNameBang",
  "gender",
  "religion",
  "maritalStatus",
  "bloodGroup",
  "birthDate",
  "age",
  "education",
  "experience",
  "nationalId",
  "licenseNo",
  "cellNo",
  "emergencyCell",
  "email",
  "presentVill",
  "presentPo",
  "presentPs",
  "presentDist",
  "presentVillBang",
  "presentPoBang",
  "presentPsBang",
  "presentDistBang",
  "permanentVill",
  "permanentPo",
  "permanentPs",
  "permanentDist",
  "permanentVillBang",
  "permanentPoBang",
  "permanentPsBang",
  "permanentDistBang",
  "nomineeName",
  "nomineeRelation",
  "nomineeCell",
  "nomineeBangla",
  "unit",
  "category",
  "department",
  "section",
  "group",
  "designation",
  "floor",
  "workingTime",
  "salaryRule",
  "grade",
  "joinDate",
  "status",
  "closeDate",
  "closeReason",
  "weekend",
  "proximityNo",
  "gross",
  "basic",
  "elSegment",
  "accountNo",
  "remarks",
  "photoPath",
  "signaturePath"
];

const CHECKBOX_FIELDS = [
  "resignGiven",
  "contractual",
  "transport",
  "otHolder",
  "elHolder",
  "quarterHolder",
  "taxHolder"
];

const DATE_FIELDS = ["birthDate", "joinDate", "closeDate"];

const DEFAULT_VALUES = {
  gender: "MALE",
  religion: "ISLAM",
  maritalStatus: "SINGLE",
  bloodGroup: "N/A",
  category: "Worker",
  status: "Active",
  weekend: "N/A",
  elSegment: "N/A",
  grade: "0"
};

const formatDateForDisplay = (value) => {
  if (!value || typeof value !== "string") {
    return "";
  }
  if (/^\d{2}-[A-Za-z]{3}-\d{4}$/.test(value)) {
    return value;
  }
  if (/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    const [year, month, day] = value.split("-");
    const months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
    const monthIndex = Number(month) - 1;
    const monthName = months[monthIndex] || "";
    return monthName ? `${day}-${monthName}-${year}` : value;
  }
  return value;
};

const parseDisplayDate = (value) => {
  if (!value || typeof value !== "string") {
    return null;
  }
  if (/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    const [year, month, day] = value.split("-").map(Number);
    return new Date(year, month - 1, day);
  }
  const match = value.match(/^(\d{2})-([A-Za-z]{3})-(\d{4})$/);
  if (match) {
    const months = {
      jan: 0,
      feb: 1,
      mar: 2,
      apr: 3,
      may: 4,
      jun: 5,
      jul: 6,
      aug: 7,
      sep: 8,
      oct: 9,
      nov: 10,
      dec: 11
    };
    const day = Number(match[1]);
    const monthIndex = months[match[2].toLowerCase()];
    const year = Number(match[3]);
    if (monthIndex !== undefined) {
      return new Date(year, monthIndex, day);
    }
  }
  return null;
};

const formatAgeDetails = (birthDate) => {
  if (!birthDate || Number.isNaN(birthDate.getTime())) {
    return "";
  }
  const today = new Date();
  let years = today.getFullYear() - birthDate.getFullYear();
  let months = today.getMonth() - birthDate.getMonth();
  let days = today.getDate() - birthDate.getDate();

  if (days < 0) {
    const lastMonth = new Date(today.getFullYear(), today.getMonth(), 0);
    days += lastMonth.getDate();
    months -= 1;
  }
  if (months < 0) {
    months += 12;
    years -= 1;
  }
  years = Math.max(years, 0);
  return `${years}y ${months}m ${days}d`;
};

const formatServiceLength = (startDate, endDate) => {
  if (!startDate || !endDate || Number.isNaN(startDate.getTime()) || Number.isNaN(endDate.getTime())) {
    return "";
  }
  let years = endDate.getFullYear() - startDate.getFullYear();
  let months = endDate.getMonth() - startDate.getMonth();
  let days = endDate.getDate() - startDate.getDate();

  if (days < 0) {
    const lastMonth = new Date(endDate.getFullYear(), endDate.getMonth(), 0);
    days += lastMonth.getDate();
    months -= 1;
  }
  if (months < 0) {
    months += 12;
    years -= 1;
  }
  years = Math.max(years, 0);
  months = Math.max(months, 0);
  days = Math.max(days, 0);
  return `${years}Y-${months}M-${days}D`;
};

const updateLengthOfService = (joinDateValue, statusValue = "", closeDateValue = "") => {
  if (!lengthOfService) {
    return;
  }
  const parsedDate = parseDisplayDate(joinDateValue);
  if (!parsedDate) {
    lengthOfService.textContent = "";
    return;
  }
  const normalizedStatus = (statusValue || "").trim().toLowerCase();
  const closeDate = parseDisplayDate(closeDateValue);
  const endDate = normalizedStatus && normalizedStatus !== "active" && closeDate ? closeDate : new Date();
  const endIso = `${endDate.getFullYear()}-${String(endDate.getMonth() + 1).padStart(2, "0")}-${String(endDate.getDate()).padStart(2, "0")}`;
  const displayDate = formatDateForDisplay(endIso);
  const duration = formatServiceLength(parsedDate, endDate);
  lengthOfService.textContent = `Length of Service (till ${displayDate}): ${duration}`;
};

const loadLeaveBalance = async (employeeCode, employeeId, status, closeDate) => {
  if (!leaveBalance) {
    return;
  }
  if (!employeeCode && !employeeId) {
    leaveBalance.textContent = "";
    return;
  }
  leaveBalance.textContent = "Leave Balance: --";
  const resolvedUnit = unit || localStorage.getItem("visorhr.unit") || employeeForm?.unit?.value || "";
  if (!resolvedUnit) {
    leaveBalance.textContent = "Leave Balance: -- (unit missing)";
    return;
  }
  const normalizedStatus = (status || "").trim().toLowerCase();
  const asOfDate = normalizedStatus && normalizedStatus !== "active" && closeDate ? closeDate : "";

  try {
    const query = new URLSearchParams({
      unit: resolvedUnit,
      code: employeeCode || "",
      empId: employeeId || "",
      asOf: asOfDate
    });
    const response = await fetch(`/employee/leave-balance?${query.toString()}`);
    const data = await response.json().catch(() => ({}));
    if (!response.ok || !data?.ok) {
      const message = data?.message || (response.status === 404 ? "endpoint not found" : "unavailable");
      leaveBalance.textContent = `Leave Balance: -- (${message})`;
      return;
    }
    const cl = data.cl ?? 0;
    const sl = data.sl ?? 0;
    const el = data.el ?? 0;
    leaveBalance.textContent = `Leave Balance: CL : ${cl} SL : ${sl}${el > 0 ? ` EL : ${el}` : ""}`;
  } catch (error) {
    leaveBalance.textContent = "Leave Balance: -- (fetch failed)";
  }
};

const LOOKUP_CONFIG = [
  { name: "unit", endpoint: "/lookup/units", placeholder: "Select unit" },
  { name: "category", endpoint: "/lookup/categories", placeholder: "Select category" },
  { name: "department", endpoint: "/lookup/departments", placeholder: "Select department" },
  { name: "section", endpoint: "/lookup/sections", placeholder: "Select section" },
  { name: "designation", endpoint: "/lookup/designations", placeholder: "Select designation" },
  { name: "group", endpoint: "/lookup/lines", placeholder: "Select group" },
  { name: "floor", endpoint: "/lookup/floors", placeholder: "Select floor" },
  { name: "salaryRule", endpoint: "/lookup/salary-rules", placeholder: "Select salary rule" }
];

const clearEmployeeForm = () => {
  if (!employeeForm) {
    return;
  }
  employeeForm.reset();
  if (employeeRowId) {
    employeeRowId.value = "";
  }
  if (employeeDelete) {
    employeeDelete.disabled = true;
  }
  if (employeeNotice) {
    employeeNotice.textContent = "";
  }
  if (lengthOfService) {
    lengthOfService.textContent = "";
  }
  if (leaveBalance) {
    leaveBalance.textContent = "";
  }
  if (employeePhotoPreview) {
    employeePhotoPreview.removeAttribute("src");
    employeePhotoPreview.classList.add("is-empty");
  }
  if (employeeSignaturePreview) {
    employeeSignaturePreview.removeAttribute("src");
    employeeSignaturePreview.classList.add("is-empty");
  }
};

const resetEmployeeFormWithCode = (code) => {
  clearEmployeeForm();
  if (employeeForm?.empCode) {
    employeeForm.empCode.value = code;
  }
};

const loadLookupOptions = (select, items, placeholder, defaultIndex = 0) => {
  if (!select) {
    return;
  }
  const currentValue = select.value;
  select.innerHTML = "";
  const placeholderOption = document.createElement("option");
  placeholderOption.value = "";
  placeholderOption.textContent = placeholder;
  select.appendChild(placeholderOption);
  items.forEach((item) => {
    const option = document.createElement("option");
    option.value = item.name;
    option.textContent = item.name;
    option.dataset.id = item.id;
    select.appendChild(option);
  });
  if (currentValue) {
    const match = Array.from(select.options).some((option) => option.value === currentValue);
    if (match) {
      select.value = currentValue;
      return;
    }
  }
  if (items.length && defaultIndex >= 0 && defaultIndex < items.length) {
    select.value = items[defaultIndex].name;
  }
};

const loadLookups = async () => {
  if (!employeeForm) {
    return;
  }
  const resolvedUnit = unit || employeeForm.unit?.value || localStorage.getItem("visorhr.unit") || "";
  if (!resolvedUnit) {
    return;
  }
  await Promise.all(
    LOOKUP_CONFIG.map(async ({ name, endpoint, placeholder }) => {
      const select = employeeForm.elements[name];
      if (!select) {
        return;
      }
      try {
        const response = await fetch(`${endpoint}?unit=${encodeURIComponent(resolvedUnit)}`);
        const data = await response.json().catch(() => ({}));
        if (!response.ok || !Array.isArray(data.items)) {
          return;
        }
        const defaultIndex = name === "unit" ? 0 : -1;
        loadLookupOptions(select, data.items, placeholder, defaultIndex);
      } catch (error) {
      }
    })
  );
};

const getEmployeeFormData = () => {
  const formData = {};
  if (!employeeForm) {
    return formData;
  }
  TEXT_FIELDS.forEach((name) => {
    const field = employeeForm.elements[name];
    if (!field) {
      return;
    }
    const value = field.value;
    formData[name] = typeof value === "string" ? value.trim() : value;
  });
  CHECKBOX_FIELDS.forEach((name) => {
    const field = employeeForm.elements[name];
    if (!field) {
      return;
    }
    formData[name] = field.checked;
  });
  formData.payType = employeeForm.querySelector("input[name='payType']:checked")?.value || "";
  formData.empCode = normalizeCode(employeeForm.empCode.value || "");
  formData.empName = employeeForm.empName.value.trim();
  return formData;
};

const applyEmployeeFormData = (employee) => {
  if (!employeeForm || !employee) {
    return;
  }
  const birthDate = employee.birthDate ? parseDisplayDate(employee.birthDate) : null;
  const ageDetails = birthDate ? formatAgeDetails(birthDate) : "";
  TEXT_FIELDS.forEach((name) => {
    const field = employeeForm.elements[name];
    if (!field) {
      return;
    }
    const value = employee[name];
    if (value === undefined || value === null || value === "") {
      field.value = DEFAULT_VALUES[name] ?? "";
      return;
    }
    if (field.tagName === "SELECT") {
      const optionExists = Array.from(field.options).some((option) => option.value === value);
      if (!optionExists) {
        const option = document.createElement("option");
        option.value = value;
        option.textContent = value;
        field.appendChild(option);
      }
    }
    if (DATE_FIELDS.includes(name)) {
      field.value = formatDateForDisplay(value);
      return;
    }
    if (name === "age" && ageDetails) {
      field.value = ageDetails;
      return;
    }
    field.value = value;
  });
  CHECKBOX_FIELDS.forEach((name) => {
    const field = employeeForm.elements[name];
    if (!field) {
      return;
    }
    field.checked = Boolean(employee[name]);
  });
  const payType = employee.payType || "Cash";
  const selectedPayType = employeeForm.querySelector(`input[name='payType'][value='${payType}']`);
  if (selectedPayType) {
    selectedPayType.checked = true;
  }
};

const setEmployeeForm = (employee) => {
  if (!employeeForm || !employee) {
    return;
  }
  applyEmployeeFormData(employee);
  if (employeeRowId) {
    employeeRowId.value = employee.id || employee.empCode || "";
  }
  if (employeeDelete) {
    employeeDelete.disabled = false;
  }
  if (employee.empCode) {
    updateEmployeePreviews(employee.empCode);
  }
  updateLengthOfService(employee.joinDate || "", employee.status || "", employee.closeDate || "");
  loadLeaveBalance(employee.empCode || employee.id || "", employee.empId || "", employee.status, employee.closeDate);
};

let employees = [];

const findEmployeeByCode = (code) => employees.find((employee) => employee.id === normalizeCode(code));

const loadEmployeeByCode = async (code) => {
  const trimmedCode = normalizeCode(code || "");
  if (!trimmedCode) {
    return;
  }

  const resolvedUnit = unit || employeeForm?.unit?.value || "";
  if (!resolvedUnit) {
    alert("Unit is required.");
    return;
  }

  try {
    const response = await fetch(`/employee/by-code?unit=${encodeURIComponent(resolvedUnit)}&code=${encodeURIComponent(trimmedCode)}`);
    if (response.status === 404) {
      resetEmployeeFormWithCode(trimmedCode);
      if (employeeNotice) {
        employeeNotice.textContent = "This employee is not exist";
      }
      alert("This employee is not exist");
      return;
    }
    const data = await response.json().catch(() => ({}));
    if (!response.ok || !data?.employee) {
      throw new Error(data.message || "Failed to load employee.");
    }

    const employee = { ...data.employee, id: trimmedCode };
    if (employee.birthDate) {
      const birth = parseDisplayDate(employee.birthDate);
      const ageDetails = birth ? formatAgeDetails(birth) : "";
      if (ageDetails) {
        employee.age = ageDetails;
      }
    }
    setEmployeeForm(employee);
    if (employeeNotice) {
      employeeNotice.textContent = "Employee loaded.";
    }
  } catch (error) {
    const message = error?.message || "Failed to load employee.";
    if (employeeNotice) {
      employeeNotice.textContent = message;
    }
    alert(message);
  }
};

const loadEmployeeImage = async (imgElement, url) => {
  if (!imgElement) {
    return;
  }
  imgElement.classList.add("is-empty");
  try {
    const response = await fetch(url);
    if (response.status === 404) {
      console.info(`Employee image not found: ${url}`);
      imgElement.removeAttribute("src");
      imgElement.classList.add("is-empty");
      return null;
    }
    if (!response.ok) {
      imgElement.removeAttribute("src");
      imgElement.classList.add("is-empty");
      return null;
    }
    const data = await response.json().catch(() => ({}));
    if (!data?.base64 || !data?.contentType) {
      imgElement.removeAttribute("src");
      imgElement.classList.add("is-empty");
      return null;
    }
    imgElement.src = `data:${data.contentType};base64,${data.base64}`;
    imgElement.classList.remove("is-empty");
    return data;
  } catch (error) {
    imgElement.removeAttribute("src");
    imgElement.classList.add("is-empty");
    return null;
  }
};

const updateEmployeePreviews = (employeeCode) => {
  const resolvedUnit = unit || employeeForm?.unit?.value || "";
  if (!resolvedUnit || !employeeCode) {
    return;
  }
  if (employeePhotoPreview) {
    loadEmployeeImage(
      employeePhotoPreview,
      `/employee/photo?unit=${encodeURIComponent(resolvedUnit)}&code=${encodeURIComponent(employeeCode)}&format=base64`
    );
  }
  if (employeeSignaturePreview) {
    loadEmployeeImage(
      employeeSignaturePreview,
      `/employee/signature?unit=${encodeURIComponent(resolvedUnit)}&code=${encodeURIComponent(employeeCode)}&format=base64`
    );
  }
};

if (employeeForm) {
  employees = getStoredEmployees();
  loadLookups();

  if (employeeForm.empCode) {
    employeeForm.empCode.addEventListener("keydown", (event) => {
      if (event.key === "Enter") {
        event.preventDefault();
        loadEmployeeByCode(employeeForm.empCode.value);
      }
    });
  }

  if (employeeForm.joinDate) {
    employeeForm.joinDate.addEventListener("input", () => {
      updateLengthOfService(
        employeeForm.joinDate.value,
        employeeForm.status?.value || "",
        employeeForm.closeDate?.value || ""
      );
    });
  }

  if (employeePhotoUpload && employeePhotoInput) {
    employeePhotoUpload.addEventListener("click", () => employeePhotoInput.click());
    employeePhotoInput.addEventListener("change", () => {
      const file = employeePhotoInput.files?.[0];
      if (!file || !employeePhotoPreview) {
        return;
      }
      const reader = new FileReader();
      reader.onload = () => {
        employeePhotoPreview.src = reader.result;
        employeePhotoPreview.classList.remove("is-empty");
        if (employeeForm?.photoPath) {
          employeeForm.photoPath.value = file.name;
        }
      };
      reader.readAsDataURL(file);
    });
  }

  if (employeeSignatureUpload && employeeSignatureInput) {
    employeeSignatureUpload.addEventListener("click", () => employeeSignatureInput.click());
    employeeSignatureInput.addEventListener("change", () => {
      const file = employeeSignatureInput.files?.[0];
      if (!file || !employeeSignaturePreview) {
        return;
      }
      const reader = new FileReader();
      reader.onload = () => {
        employeeSignaturePreview.src = reader.result;
        employeeSignaturePreview.classList.remove("is-empty");
        if (employeeForm?.signaturePath) {
          employeeForm.signaturePath.value = file.name;
        }
      };
      reader.readAsDataURL(file);
    });
  }

  employeeForm.addEventListener("submit", (event) => {
    event.preventDefault();

    const formData = getEmployeeFormData();

    if (!formData.empCode || !formData.empName) {
      if (employeeNotice) {
        employeeNotice.textContent = "Employee code and name are required.";
      }
      return;
    }

    const currentId = employeeRowId?.value || "";
    const existingIndex = employees.findIndex((item) => item.id === formData.empCode);

    if (currentId && currentId !== formData.empCode && existingIndex !== -1) {
      if (employeeNotice) {
        employeeNotice.textContent = "Employee code already exists.";
      }
      return;
    }

    if (currentId) {
      employees = employees.map((item) => (item.id === currentId ? { ...formData, id: formData.empCode } : item));
    } else if (existingIndex !== -1) {
      employees[existingIndex] = { ...formData, id: formData.empCode };
    } else {
      employees.push({ ...formData, id: formData.empCode });
    }

    saveEmployees(employees);
    clearEmployeeForm();
    if (employeeNotice) {
      employeeNotice.textContent = "Employee saved.";
    }
  });

  employeeForm.empCode.addEventListener("blur", () => {
    const code = employeeForm.empCode.value;
    if (!code) {
      clearEmployeeForm();
      return;
    }
    const employee = findEmployeeByCode(code);
    if (employee) {
      setEmployeeForm(employee);
      if (employeeNotice) {
        employeeNotice.textContent = "Employee loaded for update.";
      }
      return;
    }
    if (employeeRowId) {
      employeeRowId.value = "";
    }
    if (employeeDelete) {
      employeeDelete.disabled = true;
    }
    if (employeeNotice) {
      employeeNotice.textContent = "";
    }
  });

  if (employeeReset) {
    employeeReset.addEventListener("click", clearEmployeeForm);
  }

  if (employeeDelete) {
    employeeDelete.addEventListener("click", () => {
      const currentId = employeeRowId?.value || "";
      if (!currentId) {
        return;
    }
    employees = employees.filter((item) => item.id !== currentId);
    saveEmployees(employees);
    clearEmployeeForm();
    if (employeeNotice) {
      employeeNotice.textContent = "Employee deleted.";
    }
    });
  }
}

if (logoutLink) {
  logoutLink.addEventListener("click", () => {
    localStorage.removeItem(AUTH_STORAGE_KEY);
  });
}

const loadTotalEmployees = () => {
  if (!unit || !totalEmployees) {
    return;
  }

  fetch(`/overview/total-employees?unit=${encodeURIComponent(unit)}`)
    .then(async (response) => {
      const data = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(data.message || "Failed to load total employees.");
      }
      totalEmployees.textContent = data.totalEmp ?? "--";
    })
    .catch(() => {
      totalEmployees.textContent = "--";
    });
};

loadTotalEmployees();

const loadActiveEmployees = () => {
  if (!unit || !activeEmployees) {
    return;
  }

  fetch(`/overview/active-employees?unit=${encodeURIComponent(unit)}`)
    .then(async (response) => {
      const data = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(data.message || "Failed to load active employees.");
      }
      activeEmployees.textContent = data.activeEmp ?? "--";
    })
    .catch(() => {
      activeEmployees.textContent = "--";
    });
};

loadActiveEmployees();

const loadInactiveEmployees = () => {
  if (!unit || !inactiveEmployees) {
    return;
  }

  fetch(`/overview/inactive-employees?unit=${encodeURIComponent(unit)}`)
    .then(async (response) => {
      const data = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(data.message || "Failed to load inactive employees.");
      }
      inactiveEmployees.textContent = data.inactiveEmp ?? "--";
    })
    .catch(() => {
      inactiveEmployees.textContent = "--";
    });
};

loadInactiveEmployees();

const loadNewJoiners = () => {
  if (!unit || !newJoinersValue) {
    return;
  }

  fetch(`/overview/new-joiners?unit=${encodeURIComponent(unit)}`)
    .then(async (response) => {
      const data = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(data.message || "Failed to load new joiners.");
      }
      if (newJoinersLabel) {
        newJoinersLabel.textContent = `New Joiners (This ${data.label})`;
      }
      newJoinersValue.textContent = data.newJoiners ?? "--";
    })
    .catch(() => {
      if (newJoinersLabel) {
        newJoinersLabel.textContent = "New Joiners";
      }
      newJoinersValue.textContent = "--";
    });
};

loadNewJoiners();

const loadCloseEmployees = () => {
  if (!unit || !closeEmployees) {
    return;
  }

  fetch(`/overview/close-employees?unit=${encodeURIComponent(unit)}`)
    .then(async (response) => {
      const data = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(data.message || "Failed to load close employees.");
      }
      closeEmployees.textContent = data.closeEmp ?? "--";
    })
    .catch(() => {
      closeEmployees.textContent = "--";
    });
};

const loadReleaseResign = () => {
  if (!unit) {
    return;
  }

  fetch(`/overview/release-resign?unit=${encodeURIComponent(unit)}`)
    .then(async (response) => {
      const data = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(data.message || "Failed to load release/resign.");
      }
      if (releaseLabel) {
        releaseLabel.textContent = `Release (This ${data.label})`;
      }
      if (resignLabel) {
        resignLabel.textContent = `Resign (This ${data.label})`;
      }
      if (releaseEmployees) {
        releaseEmployees.textContent = data.releaseTotal ?? "--";
      }
      if (resignEmployees) {
        resignEmployees.textContent = data.resignCount ?? "--";
      }
    })
    .catch(() => {
      if (releaseEmployees) releaseEmployees.textContent = "--";
      if (resignEmployees) resignEmployees.textContent = "--";
      if (releaseLabel) releaseLabel.textContent = "Release";
      if (resignLabel) resignLabel.textContent = "Resign";
    });
};

const loadWorkerStaffOfficer = () => {
  if (!unit) {
    return;
  }

  fetch(`/overview/worker-staff-officer?unit=${encodeURIComponent(unit)}`)
    .then(async (response) => {
      const data = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(data.message || "Failed to load worker/staff/officer.");
      }
      if (totalWorker) totalWorker.textContent = data.totalWorker ?? "--";
      if (totalStaff) totalStaff.textContent = data.totalStaff ?? "--";
      if (totalOfficer) totalOfficer.textContent = data.totalOfficer ?? "--";
    })
    .catch(() => {
      if (totalWorker) totalWorker.textContent = "--";
      if (totalStaff) totalStaff.textContent = "--";
      if (totalOfficer) totalOfficer.textContent = "--";
    });
};

const loadGender = () => {
  if (!unit) {
    return;
  }

  fetch(`/overview/gender?unit=${encodeURIComponent(unit)}`)
    .then(async (response) => {
      const data = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(data.message || "Failed to load gender.");
      }
      if (totalMale) totalMale.textContent = data.totalMale ?? "--";
      if (totalFemale) totalFemale.textContent = data.totalFemale ?? "--";
    })
    .catch(() => {
      if (totalMale) totalMale.textContent = "--";
      if (totalFemale) totalFemale.textContent = "--";
    });
};

const loadPayHolders = () => {
  if (!unit) {
    return;
  }

  fetch(`/overview/pay-holders?unit=${encodeURIComponent(unit)}`)
    .then(async (response) => {
      const data = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(data.message || "Failed to load pay holders.");
      }
      if (cashPay) cashPay.textContent = data.cashPay ?? "--";
      if (bankPay) bankPay.textContent = data.bankPay ?? "--";
      if (mobilePay) mobilePay.textContent = data.mobilePay ?? "--";
      if (taxHolder) taxHolder.textContent = data.taxHolder ?? "--";
    })
    .catch(() => {
      if (cashPay) cashPay.textContent = "--";
      if (bankPay) bankPay.textContent = "--";
      if (mobilePay) mobilePay.textContent = "--";
      if (taxHolder) taxHolder.textContent = "--";
    });
};

const loadQuarterIncrement = () => {
  if (!unit) {
    return;
  }

  fetch(`/overview/quarter-increment?unit=${encodeURIComponent(unit)}`)
    .then(async (response) => {
      const data = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(data.message || "Failed to load quarter/increment.");
      }
      if (quarterHolder) quarterHolder.textContent = data.quarterHolder ?? "--";
      if (incrementLabel) {
        incrementLabel.textContent = `Increment (This ${data.label})`;
      }
      if (incrementValue) incrementValue.textContent = data.increment ?? "--";
    })
    .catch(() => {
      if (quarterHolder) quarterHolder.textContent = "--";
      if (incrementLabel) incrementLabel.textContent = "Increment";
      if (incrementValue) incrementValue.textContent = "--";
    });
};

const loadOffDuty = () => {
  if (!unit || !offDuty) {
    return;
  }

  fetch(`/overview/off-duty?unit=${encodeURIComponent(unit)}`)
    .then(async (response) => {
      const data = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(data.message || "Failed to load off duty.");
      }
      offDuty.textContent = data.offDuty ?? "--";
    })
    .catch(() => {
      offDuty.textContent = "--";
    });
};

const loadLeaveMaternity = () => {
  if (!unit) {
    return;
  }

  fetch(`/overview/leave-maternity?unit=${encodeURIComponent(unit)}`)
    .then(async (response) => {
      const data = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(data.message || "Failed to load leave/maternity.");
      }
      if (leaveLabel) {
        leaveLabel.textContent = `Leave (This ${data.label})`;
      }
      if (leaveValue) {
        leaveValue.textContent = `${data.leaveDays ?? 0}/${data.leaveEmp ?? 0}`;
      }
      if (maternityCount) {
        maternityCount.textContent = data.maternity ?? "--";
      }
    })
    .catch(() => {
      if (leaveLabel) leaveLabel.textContent = "Leave";
      if (leaveValue) leaveValue.textContent = "--";
      if (maternityCount) maternityCount.textContent = "--";
    });
};

loadCloseEmployees();
loadReleaseResign();
loadWorkerStaffOfficer();
loadGender();
loadPayHolders();
loadQuarterIncrement();
loadOffDuty();
loadLeaveMaternity();
