(() => {
    const form = document.getElementById("grant-application-form");
    if (!form) return;

    let initialDraft = JSON.parse(document.getElementById("draft-data")?.textContent || "{}");
    const status = document.getElementById("draft-status");
    let attachments = structuredClone(initialDraft.Attachments || []);
    let dirty = false;
    let changeVersion = 0;
    let uploadsPending = 0;
    const errorPanel = document.getElementById("application-errors");
    const errorList = document.getElementById("application-error-list");

    const getFieldErrorId = input => {
        if (!input.id) {
            const base = (input.name || "field").replace(/[^a-zA-Z0-9_-]/g, "-");
            input.id = `${base}-error-target`;
        }
        return `${input.id}-error`;
    };

    const removeFieldError = input => {
        input.classList.remove("is-invalid");
        input.removeAttribute("aria-invalid");
        const errorId = getFieldErrorId(input);
        document.getElementById(errorId)?.remove();
        form.querySelector(`[data-field-error-for='${CSS.escape(input.id)}']`)?.remove();
        errorList?.querySelector(`li[data-error-input='${CSS.escape(input.id)}']`)?.remove();
        if (errorList && errorPanel && errorList.childElementCount === 0) errorPanel.hidden = true;
        const describedBy = (input.getAttribute("aria-describedby") || "")
            .split(/\s+/)
            .filter(id => id && id !== errorId);
        if (describedBy.length) input.setAttribute("aria-describedby", describedBy.join(" "));
        else input.removeAttribute("aria-describedby");
    };

    const clearFieldErrors = () => {
        form.querySelectorAll("[aria-invalid='true']").forEach(removeFieldError);
        form.querySelectorAll(".section-error-message").forEach(error => error.remove());
        form.querySelectorAll(".section-error-indicator").forEach(indicator => indicator.remove());
        form.querySelectorAll(".section-error").forEach(section => section.classList.remove("section-error"));
    };

    const clearErrors = () => {
        clearFieldErrors();
        if (errorList) errorList.replaceChildren();
        if (errorPanel) errorPanel.hidden = true;
    };

    const fieldLabel = input => {
        if (input.getAttribute("aria-label")) return input.getAttribute("aria-label");
        if (input.id) {
            const label = form.querySelector(`label[for='${CSS.escape(input.id)}']`);
            if (label?.textContent?.trim()) return label.textContent.trim().replace(/\s*\*\s*$/, "");
        }
        const legend = input.closest("fieldset")?.querySelector("legend")?.textContent?.trim();
        return legend || "Dette feltet";
    };

    const validationMessage = input => {
        if (input.validity.valueMissing) return "Fyll ut dette feltet.";
        if (input.validity.typeMismatch || input.validity.badInput) return "Oppgi en gyldig verdi.";
        if (input.validity.rangeUnderflow) return `Verdien må være minst ${input.min}.`;
        if (input.validity.rangeOverflow) return `Verdien kan ikke være større enn ${input.max}.`;
        if (input.validity.stepMismatch) return "Oppgi en verdi med riktig trinn.";
        return input.validationMessage || "Kontroller opplysningene.";
    };

    const markFieldError = (input, message) => {
        const errorId = getFieldErrorId(input);
        input.classList.add("is-invalid");
        input.setAttribute("aria-invalid", "true");

        const label = input.id
            ? form.querySelector(`label[for='${CSS.escape(input.id)}']`)
            : null;
        if (label && !label.querySelector(`[data-field-error-for='${CSS.escape(input.id)}']`)) {
            const indicator = document.createElement("span");
            indicator.className = "field-error-indicator";
            indicator.setAttribute("data-field-error-for", input.id);
            indicator.setAttribute("aria-hidden", "true");
            indicator.textContent = "⚠";
            label.append(" ", indicator);
        } else if (!label && !form.querySelector(`[data-field-error-for='${CSS.escape(input.id)}']`)) {
            const indicator = document.createElement("span");
            indicator.className = "field-error-indicator";
            indicator.setAttribute("data-field-error-for", input.id);
            indicator.setAttribute("aria-hidden", "true");
            indicator.textContent = "⚠";
            input.before(indicator);
        }

        document.getElementById(errorId)?.remove();
        const messageElement = document.createElement("div");
        messageElement.id = errorId;
        messageElement.className = "field-error-message text-danger";
        messageElement.textContent = message;
        input.insertAdjacentElement("afterend", messageElement);
        const describedBy = (input.getAttribute("aria-describedby") || "")
            .split(/\s+/)
            .filter(id => id && id !== errorId);
        input.setAttribute("aria-describedby", [...describedBy, errorId].join(" "));
    };

    const showErrors = errors => {
        if (!errorPanel || !errorList) return;
        errorList.replaceChildren();
        errors.filter(Boolean).forEach(error => {
            const item = document.createElement("li");
            const target = typeof error === "object" ? (error.input || error.target) : null;
            const message = typeof error === "object" ? error.message : error;
            if (target) {
                if (target.id) item.dataset.errorInput = target.id;
                const link = document.createElement("a");
                link.href = target.id ? `#${target.id}` : "#application-errors";
                link.textContent = message;
                link.addEventListener("click", event => {
                    event.preventDefault();
                    if (target.matches?.("input,select,textarea")) {
                        const details = target.closest("details");
                        if (details) details.open = true;
                        showStepForInput(target);
                    } else {
                        target.scrollIntoView({ block: "center" });
                        target.querySelector?.("input,select,textarea")?.focus({ preventScroll: true });
                    }
                });
                item.append(link);
            } else {
                item.textContent = message;
            }
            errorList.append(item);
        });
        errorPanel.hidden = errorList.childElementCount === 0;
        if (!errorPanel.hidden) errorPanel.scrollIntoView({ block: "nearest" });
    };

    const markSectionError = (section, message) => {
        section.classList.add("section-error");
        const heading = section.querySelector("h2,h3,legend") || section;
        if (!heading.querySelector?.(".section-error-indicator")) {
            const indicator = document.createElement("span");
            indicator.className = "section-error-indicator";
            indicator.setAttribute("aria-hidden", "true");
            indicator.textContent = " ⚠";
            heading.append(indicator);
        }
        const messageElement = document.createElement("div");
        messageElement.className = "section-error-message text-danger";
        messageElement.textContent = message;
        section.append(messageElement);
    };

    const clearSectionError = section => {
        if (!section) return;
        section.classList.remove("section-error");
        section.querySelectorAll(".section-error-message,.section-error-indicator").forEach(error => error.remove());
    };

    const showStepForInput = input => {
        const step = input.closest("[data-application-step]");
        showApplicationStep(step?.id);
        input.focus({ preventScroll: true });
        input.scrollIntoView({ block: "center" });
    };

    const showApplicationStep = stepId => {
        document.querySelectorAll("[data-application-step]").forEach(step => {
            step.hidden = step.id !== stepId;
        });
        document.getElementById(`${stepId}-heading`)?.focus();
        window.scrollTo({ top: 0, behavior: "auto" });
    };

    const pathParts = name => name.replace(/\[(\d+)\]/g, ".$1").split(".");
    const getValue = name => pathParts(name).reduce((value, key) => value?.[key], initialDraft);

    const setValue = (target, name, value) => {
        const parts = pathParts(name);
        parts.forEach((part, index) => {
            if (index === parts.length - 1) target[part] = value;
            else target = target[part] ??= /^\d+$/.test(parts[index + 1]) ? [] : {};
        });
    };

    const markDirty = () => {
        dirty = true;
        changeVersion++;
        if (status) status.textContent = "Du har endringer som ikke er lagret.";
    };

    const toggleFields = (id, visible) => {
        const fields = document.getElementById(id);
        if (!fields) return;
        fields.hidden = !visible;
        fields.disabled = !visible;
    };

    const updateSections = () => {
        toggleFields("agreement-fields", form.querySelector('input[name="GrantType"]')?.value === "1");
        toggleFields("additional-costs-fields", form.querySelector('input[name="HasAdditionalSupervisionCosts"]:checked')?.value === "true");
    };

    const updateConditionalRequired = () => {
        const agreementVisible = form.querySelector('input[name="GrantType"]')?.value === "1";
        form.querySelectorAll(".conditional-required").forEach(el => {
            if (agreementVisible) el.classList.remove("d-none"); else el.classList.add("d-none");
        });

        const centralityYes = document.getElementById("centralityYes");
        const centralityRequiredMarkers = document.querySelectorAll("#centralitySupplementSection .text-danger");
        if (centralityYes && centralityYes.checked) {
            centralityRequiredMarkers.forEach(m => m.classList.remove("d-none"));
        } else {
            centralityRequiredMarkers.forEach(m => m.classList.add("d-none"));
        }

        const regularCheckbox = Array.from(form.querySelectorAll('input[name="SelectedPositionTypes"]')).some(cb => cb.value === "1" && cb.checked);
        const firstRegularMarker = document.querySelector('label[for="FirstRegularGpOrLocumDate"] .conditional-required');
        if (firstRegularMarker) {
            if (regularCheckbox) firstRegularMarker.classList.remove("d-none"); else firstRegularMarker.classList.add("d-none");
        }
    };

    form.addEventListener("change", updateConditionalRequired);
    form.addEventListener("change", updateSections);
    form.addEventListener("change", event => {
        const input = event.target.closest("input,select,textarea");
        if (input?.name === "SelectedPositionTypes") clearSectionError(document.getElementById("position-types"));
        if (input?.id === "agreement-upload" && attachments.some(item => item.Kind === "Agreement")) clearSectionError(document.getElementById("agreement-upload-area"));
    });
    updateConditionalRequired();
    updateSections();

    const municipalities = [...document.querySelectorAll("#norwegian-municipalities option")].map(option => option.value);
    const validateMunicipality = input => {
        const match = municipalities.find(name => name.toLocaleLowerCase("nb-NO") === input.value.trim().toLocaleLowerCase("nb-NO"));
        input.setCustomValidity(input.value.trim() && !match ? "Velg en gyldig norsk kommune fra listen." : "");
        return match;
    };

    const updatePracticeMaximumHint = () => {
        const hint = document.getElementById("practice-maximum-hint");
        const input = document.getElementById("AbsenceCompensation");
        const row = document.querySelector("#calculation-preview [data-practice-maximum]");
        if (!hint || !input) return;
        const maximum = row ? Number(row.dataset.practiceMaximum) : null;
        const isValid = Number.isFinite(input.valueAsNumber) && Number.isFinite(maximum);
        hint.hidden = !isValid || maximum === null || input.valueAsNumber <= maximum;
        hint.textContent = hint.hidden ? "" : "Maksgrense: " + maximum.toLocaleString("nb-NO", { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + " kr for sist lagrede stilling og periode. Lagre utkastet etter endringer i perioden.";
    };

    const refreshCalculation = async id => {
        const preview = document.getElementById("calculation-preview");
        const guidance = document.getElementById("guidance-preview");
        if (!preview) return;

        try {
            const url = new URL("/GrantCases/CalculationPreview", window.location.origin);
            url.searchParams.set("id", id);
            const response = await fetch(url, { headers: { "X-Requested-With": "XMLHttpRequest" } });
            if (!response.ok) {
                let message = "Beregningen kunne ikke lastes.";
                try {
                    const error = await response.json();
                    if (error?.message) message = error.message;
                } catch {
                    // Keep the generic client message when the response is not JSON.
                }
                throw new Error(message);
            }

            const html = await response.text();
            preview.innerHTML = html;
            updatePracticeMaximumHint();

            if (guidance) {
                guidance.textContent = preview.querySelector("[data-guidance-amount]")?.textContent
                    || preview.querySelector(".alert")?.textContent
                    || "Beregning er ikke tilgjengelig.";
            }
        } catch (error) {
            preview.textContent = error.message;
            updatePracticeMaximumHint();
            if (guidance) {
                guidance.textContent = error.message;
            }
        }
    };

    const buildCurrentPayload = () => {
        const payload = {
            DoctorProfessions: document.getElementById("doctor-professions")?.value.split(",").map(value => value.trim()).filter(Boolean) || [],
            SelectedPositionTypes: [],
            EmploymentPeriods: [],
            Certificate: { Sessions: [] },
            CalculationBasis: collectCalculationBasis()
        };

        for (const input of form.querySelectorAll("input[name],select[name],textarea[name]")) {
            const name = input.name;
            if (input.type === "file") continue;
            if (name === "__RequestVerificationToken" || name.endsWith(".Index")) continue;
            if (input.type === "hidden" && input.value === "false") continue;
            if (input.type === "radio" && !input.checked) continue;

            if (name === "SelectedPositionTypes") {
                if (input.checked) payload.SelectedPositionTypes.push(Number(input.value));
                continue;
            }

            let value = input.type === "checkbox" ? input.checked : input.value;
            if (input.type === "number" || input.type === "date" || name === "GrantType" || name.endsWith(".PositionType")) {
                value = value === "" ? null : input.type === "date" ? value : Number(value);
            }
            if (name === "HasAdditionalSupervisionCosts" || name === "IsCentralityGrade6") {
                value = value === "true";
            }

            setValue(payload, name, value);
        }

        payload.Attachments = attachments;
        payload.ProductivityRequestedAmount = initialDraft.ProductivityRequestedAmount ?? null;
        const hasAdditionalCosts = form.querySelector('input[name="HasAdditionalSupervisionCosts"]:checked')?.value;
        payload.HasAdditionalSupervisionCosts = hasAdditionalCosts === undefined ? null : hasAdditionalCosts === "true";
        const additionalCosts = form.querySelector('[name="AdditionalSupervisionCosts"]');
        payload.AdditionalSupervisionCosts = hasAdditionalCosts === "true" && additionalCosts?.value !== "" ? Number(additionalCosts.value) : null;
        payload.EmploymentPeriods = (payload.EmploymentPeriods || []).filter(Boolean);
        payload.Certificate.Sessions = (payload.Certificate?.Sessions || []).filter(Boolean);
        payload.Certificate.SupervisorName = initialDraft.Certificate?.SupervisorName ?? "";
        return payload;
    };

    const saveDraft = async () => {
        clearErrors();
        form.querySelectorAll(".municipality-input").forEach(input => {
            const match = validateMunicipality(input);
            input.value = match || input.value.trim();
        });

        const invalidInputs = [...form.querySelectorAll("input,select,textarea")]
            .filter(input => !input.disabled && !input.validity.valid);
        if (invalidInputs.length > 0) {
            invalidInputs.forEach(input => {
                const message = `${fieldLabel(input)}: ${validationMessage(input)}`;
                markFieldError(input, validationMessage(input));
                input.dataset.applicationErrorMessage = message;
            });
            showErrors(invalidInputs.map(input => ({ input, message: input.dataset.applicationErrorMessage })));
            const firstInvalid = invalidInputs[0];
            const details = firstInvalid.closest("details");
            if (details) details.open = true;
            showStepForInput(firstInvalid);
            firstInvalid.reportValidity();
            return null;
        }

        if (uploadsPending) {
            showErrors(["Vent til vedleggene er ferdig lest før du lagrer utkastet."]);
            if (status) status.textContent = "";
            return null;
        }

        const payload = buildCurrentPayload();
        const savedChangeVersion = changeVersion;
        const buttons = form.querySelectorAll(".save-draft");
        buttons.forEach(button => button.disabled = true);
        if (status) status.textContent = "Lagrer …";

        try {
            const requestToken = form.querySelector('[name="__RequestVerificationToken"]');
            const response = await fetch(form.action, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "RequestVerificationToken": requestToken ? requestToken.value : ""
                },
                body: JSON.stringify(payload)
            });

            const text = await response.text();
            const result = text ? (() => {
                try { return JSON.parse(text); }
                catch (error) { throw new Error("Tjeneren returnerte en ugyldig respons. Prøv igjen."); }
            })() : null;

            if (!response.ok) {
                const message = result?.message
                    || (response.status === 400 ? "Kontroller tall og datoer før du lagrer." :
                        response.status === 401 ? "Du må logge inn på nytt for å fortsette." :
                        response.status === 403 ? "Du har ikke tillatelse til å lagre dette utkastet." :
                        response.status === 500 ? "Det oppstod en serverfeil. Prøv igjen senere." :
                        "Utkastet kunne ikke lagres. Kontroller opplysningene og prøv igjen.");
                throw new Error(message);
            }

            if (!result || typeof result !== "object" || typeof result.id !== "string") {
                throw new Error("Lagringen ble ikke bekreftet av tjeneren.");
            }

            const idField = form.querySelector('[name="Id"]');
            if (idField) idField.value = result.id;

            const detailsLink = document.getElementById("case-details-navigation");
            if (detailsLink) {
                detailsLink.href = result.detailsUrl;
                detailsLink.hidden = false;
            }

            const supervisorLink = document.getElementById("supervisor-review-link");
            if (supervisorLink) {
                supervisorLink.href = "/Supervisor/Details/" + encodeURIComponent(result.id);
                supervisorLink.hidden = false;
            }

            window.history.replaceState(null, "", result.editUrl);
            initialDraft = { ...initialDraft, ...payload, Id: result.id, Attachments: payload.Attachments, InitialErrors: [] };
            dirty = false;
            clearErrors();
            if (status) status.textContent = result.message;
            await refreshCalculation(result.id);
            return result;
        } catch (error) {
            showErrors([error.message || "Utkastet kunne ikke lagres. Kontroller opplysningene og prøv igjen."]);
            if (status) status.textContent = "";
            return null;
        } finally {
            buttons.forEach(button => button.disabled = false);
            dirty = changeVersion !== savedChangeVersion;
        }
    };

    const employmentRows = document.getElementById("employment-rows");
    const employmentTemplate = document.getElementById("employment-row-template");
    const addEmploymentButton = document.getElementById("add-employment");
    if (employmentRows && employmentTemplate && addEmploymentButton) {
        let nextIndex = initialDraft.EmploymentPeriods?.length || 0;

        addEmploymentButton.addEventListener("click", () => {
            const row = document.createElement("template");
            row.innerHTML = employmentTemplate.innerHTML.replaceAll("__index__", String(nextIndex++));
            employmentRows.append(row.content.cloneNode(true));
            const employmentSection = document.querySelector(".section-error");
            if (employmentSection?.contains(employmentRows)) {
                employmentSection.classList.remove("section-error");
                employmentSection.querySelectorAll(".section-error-message,.section-error-indicator").forEach(error => error.remove());
            }
            employmentRows.lastElementChild?.querySelector("select")?.focus();
        });

        employmentRows.addEventListener("click", event => {
            if (event.target.closest(".remove-employment")) {
                const row = event.target.closest("tr");
                if (row) row.remove();
                addEmploymentButton.focus();
            }
        });
    }

    const supervisionRows = document.getElementById("supervision-rows");
    const supervisionTemplate = document.getElementById("supervision-row-template");
    const addSupervisionButton = document.getElementById("add-supervision");
    const totalSupervisionHours = document.getElementById("total-supervision-hours");
    if (supervisionRows && supervisionTemplate && addSupervisionButton && totalSupervisionHours) {
        let nextSessionIndex = initialDraft.Certificate?.Sessions?.length || 0;

        const updateTotalHours = () => {
            let hundredths = 0;
            let invalid = false;

            supervisionRows.querySelectorAll(".supervision-hours").forEach(input => {
                if (!input.validity.valid) invalid = true;
                if (Number.isFinite(input.valueAsNumber) && input.validity.valid) {
                    hundredths += Math.round(input.valueAsNumber * 100);
                }
            });

            totalSupervisionHours.textContent = invalid
                ? "Kontroller antall timer"
                : (hundredths / 100).toLocaleString("nb-NO", { maximumFractionDigits: 2 });
        };

        const addRow = focus => {
            const row = document.createElement("template");
            row.innerHTML = supervisionTemplate.innerHTML.replaceAll("__index__", String(nextSessionIndex++));
            supervisionRows.append(row.content.cloneNode(true));
            if (focus) supervisionRows.lastElementChild?.querySelector('input[type="date"]')?.focus();
        };

        addSupervisionButton.addEventListener("click", () => addRow(true));
        supervisionRows.addEventListener("input", updateTotalHours);
        supervisionRows.addEventListener("click", event => {
            const shortcut = event.target.closest("[data-hours]");
            if (shortcut) {
                const hours = shortcut.closest(".supervision-session")?.querySelector(".supervision-hours");
                if (hours) {
                    hours.value = shortcut.dataset.hours;
                    updateTotalHours();
                }
            }

            if (event.target.closest(".remove-supervision")) {
                const row = event.target.closest(".supervision-session");
                if (row) row.remove();
                updateTotalHours();
                addSupervisionButton.focus();
            }
        });

        if (!initialDraft.Certificate?.Sessions?.length) addRow(false);
    }

    const formatCalculationBasisAmount = value => `kr ${Math.round(value).toLocaleString("nb-NO")}`;
    const calculationBasisNumber = input => Number.parseFloat(input?.value.replace(/[^0-9,-]/g, "").replace(",", ".")) || 0;

    const updateCalculationBasisTotals = () => {
        document.querySelectorAll(".calculation-basis-section").forEach(section => {
            let practiceTotal = 0;
            let totalHours = 0;
            let totalDays = 0;
            let expensesTotal = 0;
            section.querySelectorAll("[data-calculation-basis-row]").forEach(row => {
                const hours = calculationBasisNumber(row.querySelector('[aria-label$="timer"]')) || calculationBasisNumber(row.querySelector('[aria-label$="dag timer"]'));
                const days = calculationBasisNumber(row.querySelector('[aria-label$="dager"]'));
                totalHours += hours;
                totalDays += days;
                const hoursAmount = hours * 825;
                const daysAmount = days * 6600;
                const total = hoursAmount + daysAmount;
                const fields = row.querySelectorAll(".calculation-basis-amount");
                if (row.querySelector('[aria-label="Sum utgift"]')) {
                    const rowExpenses = calculationBasisNumber(row.querySelector('[aria-label="Reiseutgift"]'))
                        + calculationBasisNumber(row.querySelector('[aria-label="Kursavgift"]'));
                    row.querySelector('[aria-label="Sum utgift"]').value = formatCalculationBasisAmount(rowExpenses);
                    expensesTotal += rowExpenses;
                } else if (fields.length >= 4) {
                    fields[0].value = formatCalculationBasisAmount(hoursAmount);
                    fields[1].value = formatCalculationBasisAmount(daysAmount);
                    fields[2].value = formatCalculationBasisAmount(total);
                    fields[3].value = formatCalculationBasisAmount(total);
                    practiceTotal += total;
                }
            });

            const individualRow = section.querySelector('[data-calculation-guidance="individual"]');
            const groupRow = section.querySelector('[data-calculation-guidance="group"]');
            const individualTotal = calculationBasisNumber(individualRow?.querySelector('[aria-label="Individuell veiledning timer"]')) * 825;
            const groupTotal = calculationBasisNumber(groupRow?.querySelector('[aria-label="Gruppeveiledning timer"]')) * 825
                + calculationBasisNumber(groupRow?.querySelector('[aria-label="Gruppeveiledning dager"]')) * 6600;
            if (individualRow) individualRow.querySelector(".calculation-basis-amount").value = formatCalculationBasisAmount(individualTotal);
            if (groupRow) groupRow.querySelector(".calculation-basis-amount").value = formatCalculationBasisAmount(groupTotal);
            practiceTotal += individualTotal + groupTotal;

            const practiceSum = section.querySelector('[aria-label="Total praksiskompensasjon"]');
            if (practiceSum) practiceSum.value = formatCalculationBasisAmount(practiceTotal);

            const totalHoursField = section.querySelector('[aria-label="Totalt antall timer"]');
            const totalDaysField = section.querySelector('[aria-label="Totalt antall dager"]');
            if (totalHoursField) totalHoursField.value = String(totalHours);
            if (totalDaysField) totalDaysField.value = String(totalDays);

            const expensesSum = section.querySelector('[aria-label="Totale utgifter"]');
            if (expensesSum) expensesSum.value = formatCalculationBasisAmount(expensesTotal);

            const productivityAmount = section.querySelector('[aria-label="Lavere produktivitet"]');
            const productivitySum = section.querySelector('[aria-label="Sum lavere produktivitet"]');
            if (productivityAmount && productivitySum) {
                productivitySum.value = formatCalculationBasisAmount(calculationBasisNumber(productivityAmount));
            }
        });
    };

    document.querySelectorAll("[data-add-calculation-basis-row]").forEach(button => {
        button.addEventListener("click", () => {
            const section = button.closest(".calculation-basis-section");
            const kind = button.dataset.calculationBasisKind;
            const rows = section?.querySelector("[data-calculation-basis-rows]");
            const source = rows?.querySelector(`[data-calculation-basis-row][data-calculation-basis-kind='${kind}']`);
            if (!rows || !source) return;

            const row = source.cloneNode(true);
            row.querySelectorAll("input").forEach(input => input.value = "");
            rows.insertBefore(row, button.closest("tr"));
            row.querySelector("input")?.focus();
            updateCalculationBasisTotals();
        });
    });

    document.addEventListener("click", event => {
        const button = event.target.closest("[data-remove-calculation-basis-row]");
        if (!button) return;
        const row = button.closest("[data-calculation-basis-row]");
        const rows = row?.parentElement;
        if (!row || !rows) return;
        const entryRows = rows.querySelectorAll("[data-calculation-basis-row]");
        if (entryRows.length > 1) row.remove();
        else row.querySelectorAll("input").forEach(input => input.value = "");
        updateCalculationBasisTotals();
    });

    document.addEventListener("input", event => {
        if (event.target.closest(".calculation-basis-section")) updateCalculationBasisTotals();
    });
    updateCalculationBasisTotals();

    const calculationBasisDate = input => {
        const value = input?.value?.trim();
        if (!value) return null;
        if (/^\d{4}-\d{2}-\d{2}$/.test(value)) return value;
        const match = /^(\d{1,2})\.(\d{1,2})\.(\d{2}|\d{4})$/.exec(value);
        if (!match) return null;
        const year = match[3].length === 2 ? `20${match[3]}` : match[3];
        return `${year}-${match[2].padStart(2, "0")}-${match[1].padStart(2, "0")}`;
    };

    const collectCalculationBasis = () => {
        const basis = {
            LearningActivities: [],
            StudyAndSpecializationDays: [],
            Expenses: [],
            IndividualGuidanceHours: calculationBasisNumber(document.querySelector('[aria-label="Individuell veiledning timer"]')) || null,
            GroupGuidanceHours: calculationBasisNumber(document.querySelector('[aria-label="Gruppeveiledning timer"]')) || null
        };

        document.querySelectorAll("[data-calculation-basis-row]").forEach(row => {
            const kind = row.dataset.calculationBasisKind;
            if (kind === "Expense") {
                basis.Expenses.push({
                    Description: row.querySelector('[aria-label="Utgift læringsaktivitet"]')?.value || "",
                    Date: calculationBasisDate(row.querySelector('[aria-label="Utgift dato"]')),
                    TravelExpense: calculationBasisNumber(row.querySelector('[aria-label="Reiseutgift"]')) || null,
                    CourseExpense: calculationBasisNumber(row.querySelector('[aria-label="Kursavgift"]')) || null
                });
                return;
            }

            const activity = {
                Description: row.querySelector('[aria-label="Læringsaktivitet"], [aria-label="Studie- og fordypningsdag tema"]')?.value || "",
                Date: calculationBasisDate(row.querySelector('[aria-label="Dato"], [aria-label="Studie- og fordypningsdag dato"]')),
                Hours: calculationBasisNumber(row.querySelector('[aria-label="Antall timer"], [aria-label="Studie- og fordypningsdag timer"]')) || null,
                Days: calculationBasisNumber(row.querySelector('[aria-label="Antall dager"]')) || null
            };
            if (kind === "StudyAndSpecializationDay") basis.StudyAndSpecializationDays.push(activity);
            else basis.LearningActivities.push(activity);
        });

        return basis;
    };

    const setCalculationBasisInput = (row, selector, value) => {
        const input = row.querySelector(selector);
        if (input) input.value = value ?? "";
    };

    const formatCalculationBasisDate = value => {
        if (!value) return "";
        const parts = value.split("-");
        return parts.length === 3 ? `${parts[2]}.${parts[1]}.${parts[0]}` : value;
    };

    const restoreCalculationBasisRows = (kind, items) => {
        const source = document.querySelector(`[data-calculation-basis-row][data-calculation-basis-kind='${kind}']`);
        const button = document.querySelector(`[data-add-calculation-basis-row][data-calculation-basis-kind='${kind}']`);
        const rows = source?.parentElement;
        if (!source || !button || !rows) return;

        rows.querySelectorAll(`[data-calculation-basis-row][data-calculation-basis-kind='${kind}']`).forEach((row, index) => {
            if (index > 0) row.remove();
        });

        items.forEach((item, index) => {
            const row = index === 0 ? source : source.cloneNode(true);
            if (index > 0) rows.insertBefore(row, button.closest("tr"));
            if (kind === "Expense") {
                setCalculationBasisInput(row, '[aria-label="Utgift læringsaktivitet"]', item.Description);
                setCalculationBasisInput(row, '[aria-label="Utgift dato"]', formatCalculationBasisDate(item.Date));
                setCalculationBasisInput(row, '[aria-label="Reiseutgift"]', item.TravelExpense);
                setCalculationBasisInput(row, '[aria-label="Kursavgift"]', item.CourseExpense);
            } else {
                setCalculationBasisInput(row, '[aria-label="Læringsaktivitet"], [aria-label="Studie- og fordypningsdag tema"]', item.Description);
                setCalculationBasisInput(row, '[aria-label="Dato"], [aria-label="Studie- og fordypningsdag dato"]', formatCalculationBasisDate(item.Date));
                setCalculationBasisInput(row, '[aria-label="Antall timer"], [aria-label="Studie- og fordypningsdag timer"]', item.Hours);
                setCalculationBasisInput(row, '[aria-label="Antall dager"]', item.Days);
            }
        });
    };

    restoreCalculationBasisRows("LearningActivity", initialDraft.CalculationBasis?.LearningActivities || []);
    restoreCalculationBasisRows("StudyAndSpecializationDay", initialDraft.CalculationBasis?.StudyAndSpecializationDays || []);
    restoreCalculationBasisRows("Expense", initialDraft.CalculationBasis?.Expenses || []);
    setCalculationBasisInput(document, '[aria-label="Individuell veiledning timer"]', initialDraft.CalculationBasis?.IndividualGuidanceHours);
    setCalculationBasisInput(document, '[aria-label="Gruppeveiledning timer"]', initialDraft.CalculationBasis?.GroupGuidanceHours);
    updateCalculationBasisTotals();

    const renderAttachments = () => {
        const list = document.getElementById("attachment-list");
        if (!list) return;

        list.replaceChildren();
        for (const item of attachments) {
            const row = document.createElement("li");
            const link = document.createElement("a");
            link.textContent = ({ Agreement: "ALIS-avtale", CalculationBasis: "Beregningsgrunnlag", Receipt: "Bilag" }[item.Kind] || "Vedlegg") + ": " + item.FileName;
            link.href = "#";
            link.addEventListener("click", event => {
                event.preventDefault();
                const bytes = Uint8Array.from(atob(item.ContentBase64), ch => ch.charCodeAt(0));
                const url = URL.createObjectURL(new Blob([bytes], { type: "application/octet-stream" }));
                const download = document.createElement("a");
                download.href = url;
                download.download = item.FileName;
                download.click();
                setTimeout(() => URL.revokeObjectURL(url), 1000);
            });

            row.append(link);
            if (form.dataset.readonly !== "true") {
                const remove = document.createElement("button");
                remove.type = "button";
                remove.className = "btn btn-sm btn-outline-danger ms-2";
                remove.textContent = "Fjern";
                remove.addEventListener("click", () => {
                    attachments = attachments.filter(existing => existing.Id !== item.Id);
                    renderAttachments();
                    markDirty();
                });
                row.append(remove);
            }
            list.append(row);
        }
    };

    form.querySelectorAll("[data-attachment-kind]").forEach(input => {
        input.addEventListener("change", async () => {
            uploadsPending++;
            try {
                const files = [...input.files];
                const existingBytes = attachments.reduce((sum, item) => sum + atob(item.ContentBase64).length, 0);
                const total = existingBytes + files.reduce((sum, file) => sum + file.size, 0);
                if (total > 15 * 1024 * 1024 || attachments.length + files.length > 30) {
                    throw new Error("Maksimalt 15 MB og 30 vedlegg totalt.");
                }

                for (const file of files) {
                    if (!/\.(pdf|png|jpe?g)$/i.test(file.name) || file.size === 0 || file.size > 5 * 1024 * 1024) {
                        throw new Error("Velg PDF, PNG eller JPEG på maksimalt 5 MB per fil.");
                    }
                }

                const added = await Promise.all(files.map(file => new Promise((resolve, reject) => {
                    const reader = new FileReader();
                    reader.onerror = () => reject(new Error("Kunne ikke lese vedlegget."));
                    reader.onload = () => resolve({
                        Id: crypto.randomUUID(),
                        Kind: input.dataset.attachmentKind,
                        FileName: file.name,
                        ContentBase64: String(reader.result).split(",")[1]
                    });
                    reader.readAsDataURL(file);
                })));

                attachments.push(...added);
                renderAttachments();
                if (input.dataset.attachmentKind === "Agreement") clearSectionError(document.getElementById("agreement-upload-area"));
                markDirty();
            } catch (error) {
                showErrors([error.message || "Vedlegget kunne ikke legges til."]);
                if (status) status.textContent = "";
            } finally {
                uploadsPending--;
                input.value = "";
            }
        });
    });

    const seedRows = (items, target, templateId) => {
        const container = document.getElementById(target);
        if (!items?.length || !container) return;
        container.replaceChildren();
        items.forEach((item, index) => {
            const row = document.createElement("template");
            const templateNode = document.getElementById(templateId);
            if (!templateNode) return;
            row.innerHTML = templateNode.innerHTML.replaceAll("__index__", String(index));
            container.append(row.content.cloneNode(true));
        });
    };

    seedRows(initialDraft.EmploymentPeriods, "employment-rows", "employment-row-template");
    seedRows(initialDraft.Certificate?.Sessions, "supervision-rows", "supervision-row-template");
    renderAttachments();

    for (const input of form.querySelectorAll("input[name],select[name],textarea[name]")) {
        if (input.name === "__RequestVerificationToken" || input.name.endsWith(".Index")) continue;
        const value = getValue(input.name);
        if (value === undefined || value === null) continue;
        if (input.type === "checkbox") {
            input.checked = Array.isArray(value) ? value.map(String).includes(input.value) : value === true;
        } else if (input.type === "radio") {
            input.checked = String(value) === input.value;
        } else if (!(input.type === "hidden" && input.value === "false")) {
            input.value = value;
        }
    }

    form.dispatchEvent(new Event("change"));
    const supervisionRowsForSync = document.getElementById("supervision-rows");
    supervisionRowsForSync?.dispatchEvent(new Event("input", { bubbles: true }));

    const initialErrors = [];
    (initialDraft.InitialErrors || []).forEach(error => {
        const field = error.Field
            ? form.querySelector(`[name='${CSS.escape(error.Field)}']`)
            : null;
        if (field) {
            field.required = true;
            markFieldError(field, error.Message);
            initialErrors.push({ input: field, message: `${fieldLabel(field)}: ${error.Message}` });
            return;
        }

        const section = error.Field ? document.getElementById(error.Field) : null;
        if (section) {
            markSectionError(section.closest(".table-responsive") || section, error.Message);
            initialErrors.push({ target: section.closest(".table-responsive") || section, message: error.Message });
        }
    });
    if (initialErrors.length) showErrors(initialErrors);

    const nextButton = document.getElementById("next-step");
    if (nextButton) {
        nextButton.addEventListener("click", async () => {
            const doctorNameField = document.getElementById("Certificate_DoctorName");
            if (doctorNameField && !doctorNameField.value) {
                doctorNameField.value = document.getElementById("DoctorName")?.value || "";
            }

            const saved = form.dataset.readonly === "true" ? true : await saveDraft();
            if (!saved) return;

            showApplicationStep("calculation-basis-step");
        });
    }

    const calculationBasisPreviousButton = document.getElementById("calculation-basis-previous-step");
    if (calculationBasisPreviousButton) {
        calculationBasisPreviousButton.addEventListener("click", () => showApplicationStep("application-step"));
    }

    const calculationBasisNextButton = document.getElementById("calculation-basis-next-step");
    if (calculationBasisNextButton) {
        calculationBasisNextButton.addEventListener("click", () => showApplicationStep("certificate-step"));
    }

    const previousButton = document.getElementById("previous-step");
    if (previousButton) {
        previousButton.addEventListener("click", () => showApplicationStep("calculation-basis-step"));
    }

    form.querySelectorAll("[data-case-navigation]").forEach(button => {
        button.addEventListener("click", async event => {
            event.preventDefault();
            if (button.dataset.navigationBusy === "true") return;

            const idField = form.querySelector('[name="Id"]');
            const hasSavedDraft = idField?.value && idField.value !== "00000000-0000-0000-0000-000000000000";
            button.dataset.navigationBusy = "true";
            button.setAttribute("aria-busy", "true");

            try {
                const saved = dirty || !hasSavedDraft ? await saveDraft() : null;
                if (dirty || !hasSavedDraft) {
                    if (!saved) return;
                    if (button.dataset.caseNavigation === "details" && saved.detailsUrl) {
                        button.href = saved.detailsUrl;
                    }
                }
                window.location.assign(button.href);
            } finally {
                delete button.dataset.navigationBusy;
                button.removeAttribute("aria-busy");
            }
        });
    });

    document.getElementById("AbsenceCompensation")?.addEventListener("input", updatePracticeMaximumHint);

    if (initialDraft.Id && initialDraft.Id !== "00000000-0000-0000-0000-000000000000") {
        refreshCalculation(initialDraft.Id);
    }

    if (form.dataset.readonly === "true") {
        form.querySelectorAll("input,select,textarea,button").forEach(element => {
            if (!["next-step", "previous-step"].includes(element.id)) element.disabled = true;
        });
        document.querySelectorAll("#calculation-basis-step input,#calculation-basis-step select,#calculation-basis-step textarea,#calculation-basis-step button").forEach(element => {
            if (!["calculation-basis-previous-step", "calculation-basis-next-step"].includes(element.id)) element.disabled = true;
        });
        form.querySelectorAll(".save-draft,#add-employment,#add-supervision,.remove-employment,.remove-supervision,.duration-shortcuts").forEach(element => element.hidden = true);
        document.querySelectorAll("#calculation-basis-step [data-add-calculation-basis-row],#calculation-basis-step [data-remove-calculation-basis-row]").forEach(element => element.hidden = true);
        return;
    }

    form.addEventListener("input", event => {
        markDirty();
        const input = event.target.closest("input,select,textarea");
        if (input?.validity.valid && input.getAttribute("aria-invalid") === "true") removeFieldError(input);
    });
    form.addEventListener("change", event => {
        markDirty();
        const input = event.target.closest("input,select,textarea");
        if (input?.validity.valid && input.getAttribute("aria-invalid") === "true") removeFieldError(input);
    });
    form.addEventListener("click", event => {
        if (event.target.closest("[data-hours],.remove-supervision,.remove-employment,#add-supervision,#add-employment")) {
            markDirty();
        }
    });

    window.addEventListener("beforeunload", event => {
        if (dirty) {
            event.preventDefault();
            event.returnValue = "";
        }
    });

    form.querySelectorAll(".municipality-input").forEach(input => {
        const wrapper = document.createElement("div");
        wrapper.className = "municipality-picker mb-3";
        input.before(wrapper);
        wrapper.append(input);
        input.classList.remove("mb-3");
        input.removeAttribute("list");

        const suggestions = document.createElement("div");
        suggestions.className = "municipality-suggestions";
        suggestions.id = `${input.id}-suggestions`;
        suggestions.setAttribute("role", "listbox");
        suggestions.setAttribute("aria-label", "Kommuner");
        wrapper.append(suggestions);

        input.setAttribute("role", "combobox");
        input.setAttribute("aria-autocomplete", "list");
        input.setAttribute("aria-controls", suggestions.id);

        let active = -1;
        const close = () => {
            suggestions.hidden = true;
            input.setAttribute("aria-expanded", "false");
            input.removeAttribute("aria-activedescendant");
            active = -1;
        };

        const choose = option => {
            input.value = option.textContent;
            input.dispatchEvent(new Event("input", { bubbles: true }));
            close();
        };

        const updateSuggestions = () => {
            const prefix = input.value.trim().toLocaleLowerCase("nb-NO");
            close();
            suggestions.replaceChildren(...municipalities
                .filter(name => name.toLocaleLowerCase("nb-NO").startsWith(prefix))
                .map((name, index) => {
                    const option = document.createElement("div");
                    option.id = `${suggestions.id}-${index}`;
                    option.setAttribute("role", "option");
                    option.textContent = name;
                    option.addEventListener("click", () => choose(option));
                    return option;
                }));
            suggestions.hidden = !suggestions.childElementCount;
            input.setAttribute("aria-expanded", String(!suggestions.hidden));
        };

        close();
        suggestions.addEventListener("mousedown", event => event.preventDefault());
        input.addEventListener("focus", updateSuggestions);
        input.addEventListener("blur", close);
        input.addEventListener("keydown", event => {
            if (event.key === "Escape") { close(); return; }
            if (event.key === "Enter" && active >= 0) {
                event.preventDefault();
                choose(suggestions.children[active]);
            }
            if (!["ArrowDown", "ArrowUp"].includes(event.key)) return;
            event.preventDefault();
            if (suggestions.hidden) updateSuggestions();
            const options = [...suggestions.children];
            if (!options.length) return;
            active = (active + (event.key === "ArrowDown" ? 1 : -1) + options.length) % options.length;
            options.forEach((option, index) => option.setAttribute("aria-selected", String(index === active)));
            input.setAttribute("aria-activedescendant", options[active].id);
            options[active].scrollIntoView({ block: "nearest" });
        });

        input.addEventListener("input", () => {
            updateSuggestions();
            validateMunicipality(input);
            if (input.validity.valid && input.getAttribute("aria-invalid") === "true") removeFieldError(input);
        });

        input.addEventListener("change", () => {
            const match = validateMunicipality(input);
            if (match) input.value = match;
            if (input.validity.valid && input.getAttribute("aria-invalid") === "true") removeFieldError(input);
        });
    });

    form.querySelectorAll(".save-draft").forEach(button => {
        button.addEventListener("click", async () => {
            await saveDraft();
        });
    });
})();
