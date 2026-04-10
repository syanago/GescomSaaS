(() => {
    const root = document.body;
    if (!root) {
        return;
    }

    const settings = {
        tenantCurrencyCode: (root.dataset.tenantCurrencyCode || "").toUpperCase(),
        tenantCashCurrencyCode: (root.dataset.tenantCashCurrencyCode || "").toUpperCase(),
        currencySymbol: root.dataset.currencySymbol || "",
        currencySymbolPosition: root.dataset.currencySymbolPosition || "BeforeAmount",
        moneyDecimals: Number.parseInt(root.dataset.moneyDecimals || "2", 10),
        moneyDecimalSeparator: root.dataset.moneyDecimalSeparator || ",",
        moneyGroupSeparator: root.dataset.moneyGroupSeparator ?? " ",
        quantityDecimals: Number.parseInt(root.dataset.quantityDecimals || "3", 10),
        quantityDecimalSeparator: root.dataset.quantityDecimalSeparator || ",",
        quantityGroupSeparator: root.dataset.quantityGroupSeparator ?? " ",
        serverDecimalSeparator: root.dataset.serverDecimalSeparator || ","
    };

    const editors = Array.from(document.querySelectorAll("[data-format-kind]"));

    const round = (value, decimals) => {
        const factor = 10 ** decimals;
        return Math.round((value + Number.EPSILON) * factor) / factor;
    };

    const groupDigits = (value, separator) => {
        if (!separator) {
            return value;
        }

        return value.replace(/\B(?=(\d{3})+(?!\d))/g, separator);
    };

    const formatNumber = (value, decimals, decimalSeparator, groupSeparator) => {
        const rounded = round(value, decimals);
        const sign = rounded < 0 ? "-" : "";
        const absolute = Math.abs(rounded);
        const parts = absolute.toFixed(decimals).split(".");
        const integerPart = groupDigits(parts[0], groupSeparator);

        if (decimals <= 0) {
            return `${sign}${integerPart}`;
        }

        return `${sign}${integerPart}${decimalSeparator}${parts[1]}`;
    };

    const formatServerNumber = (value, decimals) => {
        const rounded = round(value, decimals).toFixed(decimals);
        return settings.serverDecimalSeparator === "."
            ? rounded
            : rounded.replace(".", settings.serverDecimalSeparator);
    };

    const getConfig = (input) => {
        const kind = input.dataset.formatKind;
        if (kind === "money") {
            return {
                kind,
                decimals: settings.moneyDecimals,
                decimalSeparator: settings.moneyDecimalSeparator,
                groupSeparator: settings.moneyGroupSeparator
            };
        }

        if (kind === "rate") {
            return {
                kind,
                decimals: Number.parseInt(input.dataset.formatDecimals || "2", 10),
                decimalSeparator: settings.quantityDecimalSeparator,
                groupSeparator: settings.quantityGroupSeparator
            };
        }

        return {
            kind: "quantity",
            decimals: settings.quantityDecimals,
            decimalSeparator: settings.quantityDecimalSeparator,
            groupSeparator: settings.quantityGroupSeparator
        };
    };

    const normalizeInput = (rawValue, config) => {
        if (!rawValue) {
            return null;
        }

        let value = rawValue.trim()
            .replace(/\u00A0/g, " ")
            .replace(/%/g, "");

        if (settings.currencySymbol) {
            value = value.replaceAll(settings.currencySymbol, "");
        }

        if (config.groupSeparator) {
            value = value.replaceAll(config.groupSeparator, "");
        }

        value = value.replace(/\s+/g, "");

        if (config.decimalSeparator && config.decimalSeparator !== ".") {
            value = value.replaceAll(config.decimalSeparator, ".");
        }

        value = value.replace(/,/g, ".");
        value = value.replace(/[^0-9.\-]/g, "");

        const dotIndex = value.indexOf(".");
        if (dotIndex >= 0) {
            value = `${value.slice(0, dotIndex + 1)}${value.slice(dotIndex + 1).replace(/\./g, "")}`;
        }

        const parsed = Number.parseFloat(value);
        return Number.isFinite(parsed) ? parsed : null;
    };

    const formatPreview = (value, config, input) => {
        if (config.kind === "money") {
            const currencyCode = (input.dataset.formatCurrencyCode || settings.tenantCurrencyCode).toUpperCase();
            const formatted = formatNumber(Math.abs(value), config.decimals, config.decimalSeparator, config.groupSeparator);
            const sign = value < 0 ? "-" : "";
            const usesTenantSymbol = currencyCode === settings.tenantCurrencyCode || currencyCode === settings.tenantCashCurrencyCode;

            if (!usesTenantSymbol) {
                return `Rendu : ${sign}${formatted} ${currencyCode}`;
            }

            return settings.currencySymbolPosition === "BeforeAmount"
                ? `Rendu : ${sign}${settings.currencySymbol}${formatted}`
                : `Rendu : ${sign}${formatted} ${settings.currencySymbol}`;
        }

        if (config.kind === "rate") {
            return `Rendu : ${formatNumber(value, config.decimals, config.decimalSeparator, config.groupSeparator)} %`;
        }

        const formatted = formatNumber(value, config.decimals, config.decimalSeparator, config.groupSeparator);
        const unit = input.dataset.formatUnit || "";
        return unit ? `Rendu : ${formatted} ${unit}` : `Rendu : ${formatted}`;
    };

    const updateEditor = (input, normalizeDisplay) => {
        const config = getConfig(input);
        const number = normalizeInput(input.value, config);
        const preview = document.querySelector(`[data-format-preview-for="${input.id}"]`);
        const hidden = input.dataset.formatTarget
            ? document.getElementById(input.dataset.formatTarget)
            : null;

        if (number === null) {
            if (preview) {
                preview.textContent = "Rendu : --";
            }

            if (hidden) {
                hidden.value = "";
            }

            return;
        }

        if (normalizeDisplay) {
            input.value = formatNumber(number, config.decimals, config.decimalSeparator, config.groupSeparator);
        }

        if (hidden) {
            hidden.value = formatServerNumber(number, config.decimals);
        }

        if (preview) {
            preview.textContent = formatPreview(number, config, input);
        }
    };

    for (const input of editors) {
        updateEditor(input, true);

        input.addEventListener("input", () => updateEditor(input, false));
        input.addEventListener("blur", () => updateEditor(input, true));

        const form = input.closest("form");
        if (!form) {
            continue;
        }

        form.addEventListener("submit", () => {
            for (const editor of form.querySelectorAll("[data-format-kind]")) {
                updateEditor(editor, true);
            }
        });
    }

    const trackingForms = Array.from(document.querySelectorAll("[data-stock-tracking-form]"));
    const parsedSources = new Map();

    const parseTrackingSource = (sourceId) => {
        if (!sourceId) {
            return {};
        }

        if (parsedSources.has(sourceId)) {
            return parsedSources.get(sourceId);
        }

        const sourceElement = document.getElementById(sourceId);
        if (!sourceElement) {
            parsedSources.set(sourceId, {});
            return {};
        }

        try {
            const parsed = JSON.parse(sourceElement.textContent || "{}");
            const normalized = Object.fromEntries(
                Object.entries(parsed).map(([key, value]) => [key.toLowerCase(), value])
            );
            parsedSources.set(sourceId, normalized);
            return normalized;
        } catch {
            parsedSources.set(sourceId, {});
            return {};
        }
    };

    const setFieldVisibility = (field, visible) => {
        if (!field) {
            return;
        }

        field.hidden = !visible;

        for (const input of field.querySelectorAll("input, select, textarea")) {
            input.disabled = !visible;
            if (!visible) {
                input.value = "";
            }
        }
    };

    for (const form of trackingForms) {
        const select = form.querySelector("[data-stock-tracking-select]");
        if (!select) {
            continue;
        }

        const trackingMap = parseTrackingSource(form.dataset.stockTrackingSourceId || "");
        const lotField = form.querySelector("[data-stock-tracking-field='lot']");
        const lotBatchModeField = form.querySelector("[data-stock-tracking-field='lot-batch-mode']");
        const lotBatchListField = form.querySelector("[data-stock-tracking-field='lot-batch-list']");
        const lotBatchModeSelect = form.querySelector("[data-lot-entry-mode]");
        const serialField = form.querySelector("[data-stock-tracking-field='serial']");
        const expirationField = form.querySelector("[data-stock-tracking-field='expiration']");
        const serialBatchModeField = form.querySelector("[data-stock-tracking-field='serial-batch-mode']");
        const serialBatchListField = form.querySelector("[data-stock-tracking-field='serial-batch-list']");
        const serialBatchStartField = form.querySelector("[data-stock-tracking-field='serial-batch-start']");
        const serialBatchEndField = form.querySelector("[data-stock-tracking-field='serial-batch-end']");
        const serialBatchModeSelect = form.querySelector("[data-serial-entry-mode]");
        const hint = form.querySelector("[data-stock-tracking-hint]");
        const quantityHint = form.querySelector("[data-stock-tracking-quantity-hint]");
        const quantityInput = form.querySelector("[data-stock-tracking-quantity]");
        const descriptionInput = form.querySelector("[data-stock-product-description]");
        const unitCostInput = form.querySelector("[data-stock-product-cost]");
        const requiresSerializedBatch = form.dataset.requireSerializedBatch === "true";
        const requiresLotBatch = form.dataset.requireSerializedBatch === "true";

        const applyLotEntryMode = (trackingMode) => {
            const isLotBatch = trackingMode === "Lot" && requiresLotBatch;
            const selectedEntryMode = lotBatchModeSelect?.value || "Single";

            setFieldVisibility(lotField, trackingMode === "Lot" && (!requiresLotBatch || selectedEntryMode === "Single"));
            setFieldVisibility(lotBatchModeField, isLotBatch);
            setFieldVisibility(lotBatchListField, isLotBatch && selectedEntryMode === "Breakdown");
        };

        const applySerialEntryMode = (trackingMode) => {
            const isSerializedBatch = trackingMode === "SerialNumber" && requiresSerializedBatch;
            const selectedEntryMode = serialBatchModeSelect?.value || "Single";

            setFieldVisibility(serialField, trackingMode === "SerialNumber" && (!requiresSerializedBatch || selectedEntryMode === "Single"));
            setFieldVisibility(serialBatchModeField, isSerializedBatch);
            setFieldVisibility(serialBatchListField, isSerializedBatch && selectedEntryMode === "Enumeration");
            setFieldVisibility(serialBatchStartField, isSerializedBatch && selectedEntryMode === "Range");
            setFieldVisibility(serialBatchEndField, isSerializedBatch && selectedEntryMode === "Range");
        };

        const applyTrackingMode = () => {
            const selectedKey = (select.value || "").toLowerCase();
            const current = trackingMap[select.value] || trackingMap[selectedKey] || null;
            const mode = current?.mode || "None";
            const sku = current?.sku || "";
            const unit = current?.unit || "";
            const description = current?.label || "";
            const defaultPrice = Number.isFinite(Number(current?.salesPrice))
                ? Number(current.salesPrice)
                : Number.isFinite(Number(current?.purchasePrice))
                    ? Number(current.purchasePrice)
                : 0;

            if (descriptionInput) {
                if (!select.value) {
                    descriptionInput.value = "";
                } else if (current && description) {
                    descriptionInput.value = description;
                }
            }

            if (unitCostInput) {
                if (!select.value) {
                    unitCostInput.value = "";
                    updateEditor(unitCostInput, true);
                } else if (current && defaultPrice > 0) {
                    unitCostInput.value = String(defaultPrice);
                    updateEditor(unitCostInput, true);
                }
            }

            if (hint) {
                if (!select.value) {
                    hint.textContent = "Choisis un article pour afficher les champs de tracabilite utiles.";
                } else if (!current) {
                    hint.textContent = requiresSerializedBatch
                        ? "Article selectionne. Si c'est un article serialize, utilise la saisie des numeros de serie ci-dessous."
                        : "Article selectionne. Complete les champs de tracabilite si necessaire.";
                } else if (mode === "Lot") {
                    hint.textContent = requiresLotBatch
                        ? `L'article ${sku} est gere par lot. Pour cette sortie physique, saisis un lot unique ou une repartition multi-lots avec quantites.`
                        : `L'article ${sku} est gere par lot. Le lot est requis et la peremption reste disponible si tu veux tracer la DLC.`;
                } else if (mode === "SerialNumber") {
                    hint.textContent = requiresSerializedBatch
                        ? `L'article ${sku} est gere par numero de serie. Pour cette sortie physique, saisis les series par enumeration ou par plage. Une ligne sera creee par numero.`
                        : `L'article ${sku} est gere par numero de serie. Saisis un numero unique avec une quantite de 1.`;
                } else {
                    hint.textContent = `L'article ${sku} ne necessite pas de lot ni de numero de serie.`;
                }
            }

            if (quantityHint) {
                quantityHint.textContent = mode === "SerialNumber"
                    ? requiresSerializedBatch
                        ? `Chaque numero de serie cree une ligne distincte de quantite 1${unit ? ` ${unit}` : ""}.`
                        : `Regle conseille : quantite = 1${unit ? ` ${unit}` : ""} pour chaque numero de serie.`
                    : "";
            }

            setFieldVisibility(expirationField, mode === "Lot");
            applyLotEntryMode(mode);
            applySerialEntryMode(current ? mode : (requiresSerializedBatch && select.value ? "SerialNumber" : mode));
        };

        applyTrackingMode();
        select.addEventListener("change", applyTrackingMode);
        serialBatchModeSelect?.addEventListener("change", applyTrackingMode);
        lotBatchModeSelect?.addEventListener("change", applyTrackingMode);

        if (quantityInput) {
            quantityInput.addEventListener("blur", () => {
                const current = trackingMap[select.value] || null;
                const parsedQuantity = normalizeInput(quantityInput.value, getConfig(quantityInput));

                if (current?.mode === "SerialNumber" && quantityHint && parsedQuantity !== null && parsedQuantity !== 1) {
                    quantityHint.textContent = "Attention : un article gere par numero de serie doit etre saisi avec une quantite de 1.";
                } else if (current?.mode === "SerialNumber") {
                    quantityHint.textContent = `Regle conseille : quantite = 1${current?.unit ? ` ${current.unit}` : ""} pour chaque numero de serie.`;
                }
            });
        }
    }
})();

(() => {
    const schemaScript = document.getElementById("sage-schema-json");
    if (!schemaScript) {
        return;
    }

    let schemaTables = [];

    try {
        schemaTables = JSON.parse(schemaScript.textContent || "[]");
    } catch {
        schemaTables = [];
    }

    const normalize = (value) => (value || "").trim().toLowerCase();

    const getColumnsForTable = (tableName) => {
        const match = schemaTables.find((item) => normalize(item.tableName || item.TableName) === normalize(tableName));
        return match?.columns || match?.Columns || [];
    };

    const parseRawMap = (value) => {
        const lines = (value || "")
            .split(/\r?\n/)
            .map((line) => line.trim())
            .filter((line) => line.length > 0);

        const mapping = new Map();

        for (const line of lines) {
            const parts = line.split("=");
            if (parts.length < 2) {
                continue;
            }

            const target = (parts.shift() || "").trim();
            const source = parts.join("=").trim();
            if (target) {
                mapping.set(target, source);
            }
        }

        return mapping;
    };

    document.querySelectorAll("[data-sage-table-input]").forEach((tableInput) => {
        const moduleKey = tableInput.getAttribute("data-sage-table-input");
        if (!moduleKey) {
            return;
        }

        const rawMap = document.querySelector(`[data-sage-raw-map="${moduleKey}"]`);
        const fieldInputs = Array.from(document.querySelectorAll(`[data-sage-field-input="${moduleKey}"]`));
        const datalist = document.getElementById(`sage-columns-${moduleKey}`);

        if (!rawMap || fieldInputs.length === 0 || !datalist) {
            return;
        }

        const refreshColumnSuggestions = () => {
            const columns = getColumnsForTable(tableInput.value);
            datalist.innerHTML = columns
                .map((column) => `<option value="${column}"></option>`)
                .join("");
        };

        const syncFieldInputsFromRaw = () => {
            const mapping = parseRawMap(rawMap.value);
            fieldInputs.forEach((input) => {
                const targetField = input.getAttribute("data-target-field") || "";
                input.value = mapping.get(targetField) || "";
            });
        };

        const syncRawFromFieldInputs = () => {
            const knownTargets = new Set(fieldInputs.map((input) => input.getAttribute("data-target-field") || "").filter(Boolean));
            const existing = parseRawMap(rawMap.value);
            const lines = [];

            fieldInputs.forEach((input) => {
                const targetField = input.getAttribute("data-target-field") || "";
                const sourceField = input.value.trim();
                if (targetField && sourceField) {
                    lines.push(`${targetField}=${sourceField}`);
                }
            });

            existing.forEach((value, key) => {
                if (!knownTargets.has(key) && value) {
                    lines.push(`${key}=${value}`);
                }
            });

            rawMap.value = lines.join("\n");
        };

        tableInput.addEventListener("input", refreshColumnSuggestions);
        fieldInputs.forEach((input) => {
            input.addEventListener("input", syncRawFromFieldInputs);
            input.addEventListener("change", syncRawFromFieldInputs);
        });
        rawMap.addEventListener("input", syncFieldInputsFromRaw);

        refreshColumnSuggestions();
        syncFieldInputsFromRaw();
    });
})();
