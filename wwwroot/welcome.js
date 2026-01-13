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
