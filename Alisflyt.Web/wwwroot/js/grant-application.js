const initialDraft = JSON.parse(document.getElementById("draft-data")?.textContent || "{}");
(() => {
    const form = document.getElementById("grant-application-form");
    if (!form) return;

    // Save explicitly through the JSON endpoint, never a GET URL.
    form.addEventListener("submit", event => event.preventDefault());

    const toggleFields = (id, visible) => {
        const fields = document.getElementById(id);
        if (!fields) return;
        fields.hidden = !visible;
        fields.disabled = !visible;
    };

    const updateSections = () => {
        toggleFields("agreement-fields",
            form.querySelector('input[name="GrantType"]')?.value === "1");
        toggleFields("additional-costs-fields",
            form.querySelector('input[name="HasAdditionalSupervisionCosts"]:checked')?.value === "true");
    };

    // Update conditional required markers
    const updateConditionalRequired = () => {
        // Agreement fields: show/hide conditional * markers
        const agreementVisible = form.querySelector('input[name="GrantType"]')?.value === "1";
        form.querySelectorAll('.conditional-required').forEach(el => {
            if (agreementVisible) el.classList.remove('d-none'); else el.classList.add('d-none');
        });

        // Additional supervision costs
        const addCostsVisible = form.querySelector('input[name="HasAdditionalSupervisionCosts"]:checked')?.value === "true";
        document.querySelectorAll('[asp-for="AdditionalSupervisionCosts"], #additional-costs-fields .conditional-required').forEach(el => {
            // No-op: additional-costs-fields uses its own markup; handled by visibility of the fieldset
        });

        // Centrality supplement required marker handled by existing show/hide logic for section
        const centralityYes = document.getElementById('centralityYes');
        const centralityRequiredMarkers = document.querySelectorAll('#centralitySupplementSection .text-danger');
        if (centralityYes && centralityYes.checked) {
            centralityRequiredMarkers.forEach(m => m.classList.remove('d-none'));
        } else {
            centralityRequiredMarkers.forEach(m => m.classList.add('d-none'));
        }

        // FirstRegularGpOrLocumDate required when RegularGpOrLocum position type is selected
        const regularCheckbox = Array.from(form.querySelectorAll('input[name="SelectedPositionTypes"]')).some(cb => cb.value === '1' && cb.checked);
        const firstRegularMarker = document.querySelector('label[for="FirstRegularGpOrLocumDate"] .conditional-required');
        if (firstRegularMarker) {
            if (regularCheckbox) firstRegularMarker.classList.remove('d-none'); else firstRegularMarker.classList.add('d-none');
        }
    };
    form.addEventListener('change', updateConditionalRequired);
    updateConditionalRequired();
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
    let attachments = structuredClone(initialDraft.Attachments || []);
    let uploadsPending = 0;
    const renderAttachments = () => {
        const list = document.getElementById("attachment-list");
        list.replaceChildren();
        for (const item of attachments) {
            const row = document.createElement("li");
            const link = document.createElement("a");
            link.textContent = ({Agreement: "ALIS-avtale", CalculationBasis: "Beregningsgrunnlag", Receipt: "Bilag"}[item.Kind] || "Vedlegg") + ": " + item.FileName;
            link.href = "#";
            link.addEventListener("click", event => {
                event.preventDefault();
                const bytes = Uint8Array.from(atob(item.ContentBase64), ch => ch.charCodeAt(0));
                const url = URL.createObjectURL(new Blob([bytes], {type: "application/octet-stream"}));
                const download = document.createElement("a");
                download.href = url; download.download = item.FileName; download.click();
                setTimeout(() => URL.revokeObjectURL(url), 1000);
            });
            row.append(link);
            if (form.dataset.readonly !== "true") {
                const remove = document.createElement("button");
                remove.type = "button"; remove.className = "btn btn-sm btn-outline-danger ms-2";
                remove.textContent = "Fjern";
                remove.addEventListener("click", () => {
                    attachments = attachments.filter(a => a.Id !== item.Id);
                    renderAttachments(); markDirty();
                });
                row.append(remove);
            }
            list.append(row);
        }
    };
    form.querySelectorAll("[data-attachment-kind]").forEach(input => input.addEventListener("change", async () => {
        uploadsPending++;
        try {
            const files = [...input.files];
            const total = attachments.reduce((sum, a) => sum + atob(a.ContentBase64).length, 0) + files.reduce((sum, f) => sum + f.size, 0);
            if (total > 15 * 1024 * 1024 || attachments.length + files.length > 30)
                throw new Error("Maksimalt 15 MB og 30 vedlegg totalt.");
            for (const file of files) {
                if (!/\.(pdf|png|jpe?g)$/i.test(file.name) || file.size === 0 || file.size > 5 * 1024 * 1024)
                    throw new Error("Velg PDF, PNG eller JPEG på maksimalt 5 MB per fil.");
            }
            const added = await Promise.all(files.map(file => new Promise((resolve, reject) => {
                const reader = new FileReader();
                reader.onerror = () => reject(new Error("Kunne ikke lese vedlegget."));
                reader.onload = () => resolve({Id: crypto.randomUUID(), Kind: input.dataset.attachmentKind,
                    FileName: file.name, ContentBase64: String(reader.result).split(",")[1]});
                reader.readAsDataURL(file);
            })));
            attachments.push(...added); renderAttachments(); markDirty();
        } catch (error) { status.textContent = error.message; }
        finally { uploadsPending--; input.value = ""; }
    }));
    renderAttachments();
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
        const value = input.name === "GrantType" && form.dataset.readonly !== "true" ? 1 : getValue(input.name);
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

    const refreshCalculation = async id => {
        const preview = document.getElementById("calculation-preview");
        const guidance = document.getElementById("guidance-preview");
        try {
            const url = new URL("/GrantCases/CalculationPreview", window.location.origin);
            url.searchParams.set("id", id);
            const response = await fetch(url, { headers: { "X-Requested-With": "XMLHttpRequest" } });
            if (!response.ok) throw new Error("Kunne ikke hente beregningen.");
            const html = await response.text();
            preview.innerHTML = html;
            guidance.textContent = preview.querySelector("[data-guidance-amount]")?.textContent
                || preview.querySelector(".alert")?.textContent || "Beregning er ikke tilgjengelig.";
        } catch (error) {
            preview.textContent = error.message;
            guidance.textContent = error.message;
        }
    };
    if (initialDraft.Id && initialDraft.Id !== "00000000-0000-0000-0000-000000000000")
        refreshCalculation(initialDraft.Id);

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
            if (uploadsPending) { status.textContent = "Vent til vedleggene er ferdig lest."; return; }
            const payload = { DoctorProfessions: document.getElementById("doctor-professions").value.split(",").map(value => value.trim()).filter(Boolean),
                SelectedPositionTypes: [], EmploymentPeriods: [], Certificate: { Sessions: [] } };
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
                if (input.type === "number" || input.type === "date"
                    || name === "GrantType" || name.endsWith(".PositionType")) {
                    value = value === "" ? null : input.type === "date" ? value : Number(value);
                }
                if (name === "HasAdditionalSupervisionCosts"
                    || name === "IsCentralityGrade6") {
                    value = value === "true";
                }
                setValue(payload, name, value);
            }
            payload.Attachments = attachments;
            // Keep existing calculation inputs until the calculation form is updated.
            payload.SupervisionExpenses = initialDraft.SupervisionExpenses ?? null;
            payload.HasAdditionalSupervisionCosts = initialDraft.HasAdditionalSupervisionCosts ?? null;
            payload.AdditionalSupervisionCosts = initialDraft.AdditionalSupervisionCosts ?? null;
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
                const supervisorLink = document.getElementById("supervisor-review-link");
                supervisorLink.href = "/Supervisor/Details/" + encodeURIComponent(result.id);
                supervisorLink.hidden = false;
                window.history.replaceState(null, "", result.editUrl);
                dirty = changeVersion !== savedChangeVersion;
                status.textContent = dirty ? "Utkastet er lagret, men du har nye endringer som ikke er lagret." : result.message;
                await refreshCalculation(result.id);
            } catch (error) {
                status.textContent = error.message || "Lagring feilet. Prøv igjen.";
            } finally {
                buttons.forEach(b => b.disabled = false);
            }
        });
    });
})();
