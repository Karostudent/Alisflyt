(() => {
    const form = document.getElementById("grant-application-form");
    if (!form) return;

    let initialDraft = JSON.parse(document.getElementById("draft-data")?.textContent || "{}");
    const status = document.getElementById("draft-status");
    let attachments = structuredClone(initialDraft.Attachments || []);
    let dirty = false;
    let changeVersion = 0;
    let uploadsPending = 0;

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
            if (!response.ok) throw new Error("Kunne ikke hente beregningen.");

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
            Certificate: { Sessions: [] }
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
        form.querySelectorAll(".municipality-input").forEach(input => {
            const match = validateMunicipality(input);
            input.value = match || input.value.trim();
        });

        const invalid = [...form.querySelectorAll("input,select,textarea")].find(input => !input.disabled && !input.validity.valid);
        if (invalid) {
            const target = invalid.closest("#certificate-step") ? document.getElementById("next-step") : document.getElementById("previous-step");
            if (target) target.click();
            const details = invalid.closest("details");
            if (details) details.open = true;
            invalid.reportValidity();
            return null;
        }

        if (uploadsPending) {
            if (status) status.textContent = "Vent til vedleggene er ferdig lest.";
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
                        "Kunne ikke lagre utkastet.");
                throw new Error(message);
            }

            if (!result || typeof result !== "object" || typeof result.id !== "string") {
                throw new Error("Lagringen ble ikke bekreftet av tjeneren.");
            }

            const idField = form.querySelector('[name="Id"]');
            if (idField) idField.value = result.id;

            const detailsLink = document.getElementById("case-details");
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
            initialDraft = { ...initialDraft, ...payload, Id: result.id, Attachments: payload.Attachments };
            dirty = false;
            if (status) status.textContent = result.message;
            await refreshCalculation(result.id);
            return result;
        } catch (error) {
            if (status) status.textContent = error.message || "Lagring feilet. Prøv igjen.";
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
                markDirty();
            } catch (error) {
                if (status) status.textContent = error.message;
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
    const supervisionRowsForSync = document.getElementById("supervision-rows");
    supervisionRowsForSync?.dispatchEvent(new Event("input", { bubbles: true }));

    const nextButton = document.getElementById("next-step");
    if (nextButton) {
        nextButton.addEventListener("click", async () => {
            const doctorNameField = document.getElementById("Certificate_DoctorName");
            if (doctorNameField && !doctorNameField.value) {
                doctorNameField.value = document.getElementById("DoctorName")?.value || "";
            }

            const saved = await saveDraft();
            if (!saved) return;

            const applicationStep = document.getElementById("application-step");
            const certificateStep = document.getElementById("certificate-step");
            if (applicationStep) applicationStep.hidden = true;
            if (certificateStep) certificateStep.hidden = false;
            document.getElementById("certificate-heading")?.focus();
            window.scrollTo({ top: 0, behavior: "auto" });
        });
    }

    const previousButton = document.getElementById("previous-step");
    if (previousButton) {
        previousButton.addEventListener("click", () => {
            const applicationStep = document.getElementById("application-step");
            const certificateStep = document.getElementById("certificate-step");
            if (certificateStep) certificateStep.hidden = true;
            if (applicationStep) applicationStep.hidden = false;
            document.getElementById("application-heading")?.focus();
            window.scrollTo({ top: 0, behavior: "auto" });
        });
    }

    document.getElementById("AbsenceCompensation")?.addEventListener("input", updatePracticeMaximumHint);

    if (initialDraft.Id && initialDraft.Id !== "00000000-0000-0000-0000-000000000000") {
        refreshCalculation(initialDraft.Id);
    }

    if (form.dataset.readonly === "true") {
        form.querySelectorAll("input,select,textarea,button").forEach(element => {
            if (!["next-step", "previous-step"].includes(element.id)) element.disabled = true;
        });
        form.querySelectorAll(".save-draft,#add-employment,#add-supervision,.remove-employment,.remove-supervision,.duration-shortcuts").forEach(element => element.hidden = true);
        return;
    }

    form.addEventListener("input", markDirty);
    form.addEventListener("change", markDirty);
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
        });

        input.addEventListener("change", () => {
            const match = validateMunicipality(input);
            if (match) input.value = match;
        });
    });

    form.querySelectorAll(".save-draft").forEach(button => {
        button.addEventListener("click", async () => {
            await saveDraft();
        });
    });
})();
