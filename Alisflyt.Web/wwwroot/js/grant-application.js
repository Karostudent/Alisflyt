const initialDraft = JSON.parse(document.getElementById("draft-data")?.textContent || "{}");
(() => {
    const form = document.getElementById("grant-application-form");
    if (!form) return;

    // Save explicitly through the JSON endpoint, never a GET URL.
    form.addEventListener("submit", event => event.preventDefault());

    const toggleFields = (id, visible) => {
        const fields = document.getElementById(id);
        fields.hidden = !visible;
        fields.disabled = !visible;
    };

    const updateSections = () => {
        toggleFields("agreement-fields",
            form.querySelector('input[name="GrantType"]:checked')?.value === "1");
        toggleFields("additional-costs-fields",
            form.querySelector('input[name="HasAdditionalSupervisionCosts"]:checked')?.value === "true");
    };
    form.addEventListener("change", updateSections);
    updateSections();

    let nextIndex = initialDraft.EmploymentPeriods?.length || 0;
    const rows = document.getElementById("employment-rows");
    const template = document.getElementById("employment-row-template");
    const addButton = document.getElementById("add-employment");

    addButton.addEventListener("click", () => {
        const row = document.createElement("template");
        row.innerHTML = template.innerHTML.replaceAll("__index__", String(nextIndex++));
        rows.append(row.content.cloneNode(true));
        rows.lastElementChild.querySelector("select").focus();
    });

    rows.addEventListener("click", event => {
        if (event.target.closest(".remove-employment")) {
            event.target.closest("tr").remove();
            addButton.focus();
        }
    });
})();

(() => {
    const next = document.getElementById("next-step");
    if (!next) return;
    const application = document.getElementById("application-step");
    const certificate = document.getElementById("certificate-step");
    const doctorName = document.getElementById("Certificate_DoctorName");

    next.addEventListener("click", () => {
        if (!doctorName.value) {
            doctorName.value = document.getElementById("DoctorName").value;
        }
        application.hidden = true;
        certificate.hidden = false;
        document.getElementById("certificate-heading").focus();
        window.scrollTo({ top: 0, behavior: "auto" });
    });

    document.getElementById("previous-step").addEventListener("click", () => {
        certificate.hidden = true;
        application.hidden = false;
        document.getElementById("application-heading").focus();
        window.scrollTo({ top: 0, behavior: "auto" });
    });

    let nextSessionIndex = initialDraft.Certificate?.Sessions?.length || 0;
    const rows = document.getElementById("supervision-rows");
    const template = document.getElementById("supervision-row-template");
    const add = document.getElementById("add-supervision");
    const total = document.getElementById("total-supervision-hours");

    const updateTotal = () => {
        let hundredths = 0;
        let invalid = false;
        rows.querySelectorAll(".supervision-hours").forEach(input => {
            if (!input.validity.valid) invalid = true;
            if (Number.isFinite(input.valueAsNumber) && input.validity.valid) {
                hundredths += Math.round(input.valueAsNumber * 100);
            }
        });
        total.textContent = invalid ? "Kontroller antall timer" :
            (hundredths / 100).toLocaleString("nb-NO", { maximumFractionDigits: 2 });
    };

    const addRow = (focus) => {
        const row = document.createElement("template");
        row.innerHTML = template.innerHTML.replaceAll("__index__", String(nextSessionIndex++));
        rows.append(row.content.cloneNode(true));
        if (focus) rows.lastElementChild.querySelector('input[type="date"]').focus();
    };
    add.addEventListener("click", () => addRow(true));
    rows.addEventListener("input", updateTotal);
    rows.addEventListener("click", event => {

        const shortcut = event.target.closest("[data-hours]");
        if (shortcut) {
            const hours = shortcut.closest(".supervision-session").querySelector(".supervision-hours");
            hours.value = shortcut.dataset.hours;
            updateTotal();
        }
        if (event.target.closest(".remove-supervision")) {
            event.target.closest(".supervision-session").remove();
            updateTotal();
            add.focus();
        }
    });
    if (!initialDraft.Certificate?.Sessions?.length) addRow(false);
})();

(() => {
    const form = document.getElementById("grant-application-form");
    if (!form) return;
    const status = document.getElementById("draft-status");
    let dirty = false;
    let changeVersion = 0;
    const markDirty = () => {
        dirty = true;
        changeVersion++;
        status.textContent = "Du har endringer som ikke er lagret.";
    };

    const seedRows = (items, target, templateId) => {
        if (!items?.length) return;
        const container = document.getElementById(target);
        container.replaceChildren();
        items.forEach((item, index) => {
            const template = document.createElement("template");
            template.innerHTML = document.getElementById(templateId).innerHTML.replaceAll("__index__", String(index));
            container.append(template.content.cloneNode(true));
        });
    };
    seedRows(initialDraft.EmploymentPeriods, "employment-rows", "employment-row-template");
    seedRows(initialDraft.Certificate?.Sessions, "supervision-rows", "supervision-row-template");

    const pathParts = name => name.replace(/\[(\d+)\]/g, ".$1").split(".");
    const getValue = name => pathParts(name).reduce((value, key) => value?.[key], initialDraft);
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
    document.getElementById("supervision-rows").dispatchEvent(new Event("input", { bubbles: true }));

    if (form.dataset.readonly === "true") {
        form.querySelectorAll("input,select,textarea,button").forEach(input => {
            if (!["next-step", "previous-step"].includes(input.id)) input.disabled = true;
        });
        form.querySelectorAll(".save-draft,#add-employment,#add-supervision,.remove-employment,.remove-supervision,.duration-shortcuts").forEach(element => element.hidden = true);
        return;
    }
    form.addEventListener("input", markDirty);
    form.addEventListener("change", markDirty);
    form.addEventListener("click", event => {
        if (event.target.closest("[data-hours],.remove-supervision,.remove-employment,#add-supervision,#add-employment")) markDirty();
    });
    window.addEventListener("beforeunload", event => {
        if (dirty) { event.preventDefault(); event.returnValue = ""; }
    });

    const setValue = (target, name, value) => {
        const parts = pathParts(name);
        parts.forEach((part, index) => {
            if (index === parts.length - 1) target[part] = value;
            else target = target[part] ??= /^\d+$/.test(parts[index + 1]) ? [] : {};
        });
    };

    const municipalities = [...document.querySelectorAll("#norwegian-municipalities option")].map(option => option.value);
    const validateMunicipality = input => {
        const match = municipalities.find(name => name.toLocaleLowerCase("nb-NO") === input.value.trim().toLocaleLowerCase("nb-NO"));
        input.setCustomValidity(input.value.trim() && !match ? "Velg en gyldig norsk kommune fra listen." : "");
        return match;
    };
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
        });
        input.addEventListener("change", () => {
            const match = validateMunicipality(input);
            if (match) input.value = match;
        });
    });

    form.querySelectorAll(".save-draft").forEach(button => {
        button.addEventListener("click", async () => {
            form.querySelectorAll(".municipality-input").forEach(input => {
                const match = validateMunicipality(input);
                input.value = match || input.value.trim();
            });
            const invalid = [...form.querySelectorAll("input,select,textarea")].find(input =>
                !input.disabled && !input.validity.valid);
            if (invalid) {
                const onCertificate = Boolean(invalid.closest("#certificate-step"));
                document.getElementById(onCertificate ? "next-step" : "previous-step").click();
                const details = invalid.closest("details");
                if (details) details.open = true;
                invalid.reportValidity();
                return;
            }
            const payload = { DoctorProfessions: document.getElementById("doctor-professions").value.split(",").map(value => value.trim()).filter(Boolean),
                SelectedPositionTypes: [], EmploymentPeriods: [], Certificate: { Sessions: [] } };
            for (const input of form.querySelectorAll("input[name],select[name],textarea[name]")) {
                const name = input.name;
                if (name === "__RequestVerificationToken" || name.endsWith(".Index")) continue;
                if (input.type === "hidden" && input.value === "false") continue;
                if (input.type === "radio" && !input.checked) continue;
                if (name === "SelectedPositionTypes") {
                    if (input.checked) payload.SelectedPositionTypes.push(Number(input.value));
                    continue;
                }
                let value = input.type === "checkbox" ? input.checked : input.value;
                if (input.type === "number" || input.type === "date"
                    || name === "GrantType" || name.endsWith(".PositionType")) {
                    value = value === "" ? null : input.type === "date" ? value : Number(value);
                }
                if (name === "HasAdditionalSupervisionCosts") value = value === "true";
                setValue(payload, name, value);
            }
            payload.EmploymentPeriods = payload.EmploymentPeriods.filter(Boolean);
            payload.Certificate.Sessions = payload.Certificate.Sessions.filter(Boolean);
            const savedChangeVersion = changeVersion;
            const buttons = form.querySelectorAll(".save-draft");
            buttons.forEach(b => b.disabled = true);
            status.textContent = "Lagrer …";
            try {
                const response = await fetch(form.action, {
                    method: "POST",
                    headers: { "Content-Type": "application/json",
                        "RequestVerificationToken": form.querySelector('[name="__RequestVerificationToken"]').value },
                    body: JSON.stringify(payload)
                });
                const result = await response.json();
                if (!response.ok) throw new Error(result.message || "Kunne ikke lagre utkastet.");
                form.querySelector('[name="Id"]').value = result.id;
                const details = document.getElementById("case-details");
                details.href = result.detailsUrl;
                details.hidden = false;
                window.history.replaceState(null, "", result.editUrl);
                dirty = changeVersion !== savedChangeVersion;
                status.textContent = dirty ? "Utkastet er lagret, men du har nye endringer som ikke er lagret." : result.message;
            } catch (error) {
                status.textContent = error.message || "Lagring feilet. Prøv igjen.";
            } finally {
                buttons.forEach(b => b.disabled = false);
            }
        });
    });
})();
