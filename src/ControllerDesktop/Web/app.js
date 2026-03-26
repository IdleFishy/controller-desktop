const CONTROL_OPTIONS = [



  ["A", "A 键"],



  ["B", "B 键"],



  ["X", "X 键"],



  ["Y", "Y 键"],



  ["DPadUp", "方向键上"],



  ["DPadDown", "方向键下"],



  ["DPadLeft", "方向键左"],



  ["DPadRight", "方向键右"],



  ["LeftShoulder", "LB"],



  ["RightShoulder", "RB"],



  ["LeftTrigger", "LT"],



  ["RightTrigger", "RT"],



  ["Start", "开始键"],



  ["Back", "返回键"],

  ["LeftThumb", "左摇杆按下"],

  ["RightThumb", "右摇杆按下"],



  ["LeftStickX", "左摇杆横向"],



  ["LeftStickY", "左摇杆纵向"],



  ["RightStickX", "右摇杆横向"],



  ["RightStickY", "右摇杆纵向"]



];







const TRIGGER_MODE_OPTIONS = [



  ["Button", "按钮"],



  ["AxisPositive", "正向轴"],



  ["AxisNegative", "反向轴"]



];







const REPEAT_MODE_OPTIONS = [



  ["OnPress", "按下触发"],



  ["WhileHeld", "按住持续"],



  ["Analog", "模拟量"]



];







const ACTION_TYPE_OPTIONS = [



  ["KeyboardKey", "键盘单键"],



  ["KeyboardChord", "键盘组合键"],



  ["MouseMove", "鼠标移动"],



  ["MouseButton", "鼠标按钮"],



  ["MouseWheel", "鼠标滚轮"],



  ["SystemAction", "系统动作"]



];







const SYSTEM_ACTION_OPTIONS = [



  ["VolumeUp", "音量增加"],



  ["VolumeDown", "音量减少"],



  ["VolumeMute", "静音 / 取消静音"],



  ["ShowDesktop", "显示桌面"],



  ["TaskView", "任务视图"]



];







const BEHAVIOR_LABELS = {



  SinglePress: "单击",



  DoublePress: "双击",



  LongPress: "长按",



  Hold: "持续"



};







const BEHAVIOR_DETAILS = {



  SinglePress: "单击：只在首次按下的瞬间触发一次。适合确认、点击、打开任务视图这类即时动作。",



  DoublePress: "双击：需要在约 320ms 内连续触发两次，第二次成立后才执行动作。适合减少误触。",



  LongPress: "长按：按住超过约 420ms 才生效。若动作是键盘按住或鼠标按住，会从这时开始保持，到松开结束；若动作是点击类，只触发一次。",



  Hold: "持续：按下就立刻进入持续态，松开马上结束。适合方向键持续移动、拖拽、鼠标长按、连续滚轮。"



};







const STATUS_TEXT = {



  ActiveDesktop: ["桌面映射生效中", "success"],



  SuspendedByGame: ["已检测到全屏前台", "warn"],



  SuspendedByLockScreen: ["锁屏中", "warn"],



  SuspendedByNoController: ["等待手柄连接", "danger"]



};







const SYSTEM_ACTION_CANONICAL = {



  VolumeUp: "VolumeUp",



  音量增加: "VolumeUp",



  增大音量: "VolumeUp",



  VolumeDown: "VolumeDown",



  音量减少: "VolumeDown",



  降低音量: "VolumeDown",



  VolumeMute: "VolumeMute",



  Mute: "VolumeMute",



  静音: "VolumeMute",



  音量静音: "VolumeMute",



  ShowDesktop: "ShowDesktop",



  显示桌面: "ShowDesktop",



  TaskView: "TaskView",



  任务视图: "TaskView"



};







const state = {



  currentConfig: null,



  selectedRuleId: null,



  dirty: false,



  sourceLabel: "应用配置",



  capture: {



    active: false,



    pollTimer: null



  }



};







const refs = {



  sourceBadge: document.getElementById("sourceBadge"),



  configPath: document.getElementById("configPath"),



  runtimeState: document.getElementById("runtimeState"),



  foregroundProcess: document.getElementById("foregroundProcess"),



  recentAction: document.getElementById("recentAction"),



  controllerState: document.getElementById("controllerState"),



  dirtyState: document.getElementById("dirtyState"),



  runtimeEnabledToggle: document.getElementById("runtimeEnabledToggle"),



  autostartToggle: document.getElementById("autostartToggle"),



  profileTitle: document.getElementById("profileTitle"),



  profileNameInput: document.getElementById("profileNameInput"),



  controllerSlotSelect: document.getElementById("controllerSlotSelect"),



  ruleList: document.getElementById("ruleList"),



  editorHint: document.getElementById("editorHint"),



  displayNameInput: document.getElementById("displayNameInput"),



  controlSelect: document.getElementById("controlSelect"),



  triggerModeSelect: document.getElementById("triggerModeSelect"),



  repeatModeSelect: document.getElementById("repeatModeSelect"),



  actionTypeSelect: document.getElementById("actionTypeSelect"),



  thresholdInput: document.getElementById("thresholdInput"),



  parameterRow: document.getElementById("parameterRow"),



  parameterInput: document.getElementById("parameterInput"),



  systemActionRow: document.getElementById("systemActionRow"),



  systemActionSelect: document.getElementById("systemActionSelect"),



  sensitivityInput: document.getElementById("sensitivityInput"),



  ruleEnabledToggle: document.getElementById("ruleEnabledToggle"),



  mappingSummary: document.getElementById("mappingSummary"),



  baseSpeedInput: document.getElementById("baseSpeedInput"),



  accelerationInput: document.getElementById("accelerationInput"),



  scrollStepInput: document.getElementById("scrollStepInput"),



  deadZoneInput: document.getElementById("deadZoneInput"),



  behaviorDescription: document.getElementById("behaviorDescription"),



  captureStrip: document.getElementById("captureStrip"),



  captureButton: document.getElementById("captureButton"),



  clearParameterButton: document.getElementById("clearParameterButton"),



  captureHint: document.getElementById("captureHint"),



  toast: document.getElementById("toast"),



  fileInput: document.getElementById("fileInput")



};







let toastTimer = null;







bootstrap();







function bootstrap() {



  fillSelect(refs.controlSelect, CONTROL_OPTIONS);



  fillSelect(refs.triggerModeSelect, TRIGGER_MODE_OPTIONS);



  fillSelect(refs.repeatModeSelect, REPEAT_MODE_OPTIONS);



  fillSelect(refs.actionTypeSelect, ACTION_TYPE_OPTIONS);



  fillSelect(refs.systemActionSelect, SYSTEM_ACTION_OPTIONS);



  bindGlobalActions();



  bindFormInputs();



  loadAppConfiguration();



  refreshRuntimeStatus();



  window.setInterval(refreshRuntimeStatus, 1800);



}







function fillSelect(select, options) {



  select.innerHTML = options.map(([value, label]) => `<option value="${value}">${label}</option>`).join("");



}







function bindGlobalActions() {



  document.getElementById("reloadButton").addEventListener("click", loadAppConfiguration);



  document.getElementById("importButton").addEventListener("click", () => refs.fileInput.click());



  document.getElementById("saveAppButton").addEventListener("click", saveToApplication);



  document.getElementById("exportButton").addEventListener("click", exportConfiguration);



  document.getElementById("applyDesktopMousePresetButton")?.addEventListener("click", applyDesktopMousePreset);



  document.getElementById("addRuleButton").addEventListener("click", addRule);



  document.getElementById("deleteRuleButton").addEventListener("click", deleteSelectedRule);



  refs.fileInput.addEventListener("change", importLocalFile);



  refs.captureButton.addEventListener("click", toggleCapture);



  refs.clearParameterButton.addEventListener("click", clearCapturedParameter);







  document.querySelectorAll(".trigger-palette button").forEach((button) => {



    button.addEventListener("click", () => {



      const rule = getSelectedRule();



      if (!rule) {



        return;



      }







      rule.triggerBehavior = button.dataset.behavior;



      if (rule.triggerBehavior === "Hold" && rule.repeatMode === "OnPress") {



        rule.repeatMode = "WhileHeld";



      }



      if (rule.triggerBehavior !== "Hold" && rule.repeatMode === "Analog") {



        rule.repeatMode = "OnPress";



      }



      markDirty();



      syncEditor();



      renderRuleList();



      renderSummary();



    });



  });



}







function bindFormInputs() {



  refs.runtimeEnabledToggle.addEventListener("change", () => {



    if (!state.currentConfig) {



      return;



    }



    state.currentConfig.runtimeEnabled = refs.runtimeEnabledToggle.checked;



    markDirty();



  });







  refs.autostartToggle.addEventListener("change", () => {



    if (!state.currentConfig) {



      return;



    }



    state.currentConfig.startWithWindows = refs.autostartToggle.checked;



    markDirty();



  });







  refs.profileNameInput.addEventListener("input", () => {



    const profile = getProfile();



    if (!profile) {



      return;



    }



    profile.name = refs.profileNameInput.value.trim() || "配置 1";



    refs.profileTitle.textContent = profile.name;



    markDirty();



    renderRuleList();



  });







  refs.controllerSlotSelect.addEventListener("change", () => updateProfile((profile) => {



    profile.controllerSlot = Number(refs.controllerSlotSelect.value);



  }));







  bindCursorInput(refs.baseSpeedInput, "baseSpeed");



  bindCursorInput(refs.accelerationInput, "acceleration");



  bindCursorInput(refs.scrollStepInput, "scrollStep");



  bindCursorInput(refs.deadZoneInput, "deadZone");







  refs.displayNameInput.addEventListener("input", () => updateSelectedRule((rule) => {



    rule.displayName = refs.displayNameInput.value.trim() || readableControl(rule.trigger.control, rule.trigger.mode);



  }, true));







  refs.controlSelect.addEventListener("change", () => updateSelectedRule((rule) => {



    rule.trigger.control = refs.controlSelect.value;



    if (!rule.displayName || looksAutoNamed(rule.displayName)) {



      rule.displayName = readableControl(rule.trigger.control, rule.trigger.mode);



    }



  }, true));







  refs.triggerModeSelect.addEventListener("change", () => updateSelectedRule((rule) => {



    rule.trigger.mode = refs.triggerModeSelect.value;



    if (looksAutoNamed(rule.displayName)) {



      rule.displayName = readableControl(rule.trigger.control, rule.trigger.mode);



    }



  }, true));







  refs.repeatModeSelect.addEventListener("change", () => updateSelectedRule((rule) => {



    rule.repeatMode = refs.repeatModeSelect.value;



  }, true));







  refs.actionTypeSelect.addEventListener("change", () => updateSelectedRule((rule) => {



    rule.action.type = refs.actionTypeSelect.value;



    if (rule.action.type === "SystemAction") {



      rule.action.parameter = normalizeSystemAction(rule.action.parameter || refs.systemActionSelect.value || "VolumeUp") || "VolumeUp";



    } else if (rule.action.type === "MouseWheel" && !rule.action.parameter) {



      rule.action.parameter = "Vertical";



    }



  }, true));







  refs.systemActionSelect.addEventListener("change", () => updateSelectedRule((rule) => {



    rule.action.parameter = refs.systemActionSelect.value;



  }, true));







  refs.thresholdInput.addEventListener("input", () => updateSelectedRule((rule) => {



    rule.trigger.threshold = safeNumber(refs.thresholdInput.value, 0.45);



  }));







  refs.parameterInput.addEventListener("input", () => updateSelectedRule((rule) => {



    rule.action.parameter = refs.parameterInput.value.trim();



  }, true));







  refs.sensitivityInput.addEventListener("input", () => updateSelectedRule((rule) => {



    rule.action.sensitivity = safeNumber(refs.sensitivityInput.value, 1);



  }));







  refs.ruleEnabledToggle.addEventListener("change", () => updateSelectedRule((rule) => {



    rule.isEnabled = refs.ruleEnabledToggle.checked;



  }, true));



}







function bindCursorInput(input, key) {



  input.addEventListener("input", () => {



    const settings = getCursorSettings();



    if (!settings) {



      return;



    }



    settings[key] = safeNumber(input.value, settings[key]);



    markDirty();



  });



}







async function loadAppConfiguration() {



  try {



    const response = await fetch("/api/config", { cache: "no-store" });



    if (!response.ok) {



      throw new Error("读取应用配置失败");



    }







    const payload = await response.json();



    state.currentConfig = normalizeConfiguration(payload.configuration ?? payload);



    state.sourceLabel = "应用配置";



    state.selectedRuleId = state.currentConfig.profile.rules[0]?.id ?? null;



    state.dirty = false;



    refs.sourceBadge.textContent = state.sourceLabel;



    refs.configPath.textContent = payload.configPath ?? "本地文件";



    renderAll();



    showToast("已读取应用当前配置。", "success");



  } catch (error) {



    showToast(error.message || "读取应用配置失败。", "error");



  }



}







async function refreshRuntimeStatus() {



  try {



    const response = await fetch("/api/status", { cache: "no-store" });



    if (!response.ok) {



      return;



    }







    const status = await response.json();



    const [text, tone] = STATUS_TEXT[status.activationState] || ["状态未知", "warn"];



    refs.runtimeState.textContent = text;



    refs.runtimeState.className = `status-chip status-chip-${tone}`;



    refs.foregroundProcess.textContent = status.foregroundProcessName || "-";



    refs.recentAction.textContent = status.recentAction || "-";



    refs.controllerState.textContent = status.isControllerConnected



      ? `已连接（控制器 ${Number(status.activeControllerSlot ?? 0) + 1}）`



      : "未连接";



  } catch {



    refs.runtimeState.textContent = "状态读取失败";



    refs.runtimeState.className = "status-chip status-chip-danger";



  }



}







async function saveToApplication() {



  if (!state.currentConfig) {



    return;



  }







  try {



    const response = await fetch("/api/config", {



      method: "PUT",



      headers: { "Content-Type": "application/json" },



      body: JSON.stringify(normalizeConfiguration(state.currentConfig))



    });







    if (!response.ok) {



      const data = await response.json().catch(() => ({}));



      throw new Error(data.message || "保存失败");



    }







    state.dirty = false;



    renderDirtyState();



    refs.sourceBadge.textContent = "应用配置";



    state.sourceLabel = "应用配置";



    showToast("已保存到应用本地配置。", "success");



  } catch (error) {



    showToast(error.message || "保存失败。", "error");



  }



}







function exportConfiguration() {



  if (!state.currentConfig) {



    return;



  }







  const blob = new Blob([JSON.stringify(state.currentConfig, null, 2)], { type: "application/json" });



  const url = URL.createObjectURL(blob);



  const anchor = document.createElement("a");



  anchor.href = url;



  anchor.download = `controller-desktop-${Date.now()}.json`;



  anchor.click();



  URL.revokeObjectURL(url);



}







async function importLocalFile(event) {



  const [file] = event.target.files || [];



  if (!file) {



    return;



  }







  try {



    const text = await file.text();



    const raw = JSON.parse(text);



    state.currentConfig = normalizeConfiguration(raw.configuration ?? raw);



    state.sourceLabel = `已导入: ${file.name}`;



    refs.sourceBadge.textContent = state.sourceLabel;



    refs.configPath.textContent = file.name;



    state.selectedRuleId = state.currentConfig.profile.rules[0]?.id ?? null;



    state.dirty = true;



    renderAll();



    showToast("本地配置已导入。", "success");



  } catch (error) {



    showToast(error.message || "导入失败。", "error");



  } finally {



    refs.fileInput.value = "";



  }



}







function addRule() {



  const profile = getProfile();



  if (!profile) {



    return;



  }







  const rule = createRule();



  profile.rules.push(rule);



  state.selectedRuleId = rule.id;



  markDirty();



  renderAll();



}







function applyDesktopMousePreset() {



  const profile = getProfile();



  if (!profile) {



    return;



  }







  const horizontalRule = upsertRule(



    profile,



    (rule) => rule.trigger.control === "LeftStickX" && rule.repeatMode === "Analog" && rule.action.type === "MouseMove" && String(rule.action.parameter).toUpperCase() === "X",



    () => ({



      id: randomId(),



      displayName: "?????",



      isEnabled: true,



      repeatMode: "Analog",



      triggerBehavior: "Hold",



      trigger: { control: "LeftStickX", mode: "AxisPositive", threshold: 0.01 },



      action: { type: "MouseMove", parameter: "X", sensitivity: 1 }



    })



  );







  horizontalRule.displayName = "左摇杆横向";



  horizontalRule.isEnabled = true;



  horizontalRule.repeatMode = "Analog";



  horizontalRule.triggerBehavior = "Hold";



  horizontalRule.trigger = { control: "LeftStickX", mode: "AxisPositive", threshold: 0.01 };



  horizontalRule.action = { type: "MouseMove", parameter: "X", sensitivity: 1 };







  const verticalRule = upsertRule(



    profile,



    (rule) => rule.trigger.control === "LeftStickY" && rule.repeatMode === "Analog" && rule.action.type === "MouseMove" && String(rule.action.parameter).toUpperCase() === "Y",



    () => ({



      id: randomId(),



      displayName: "?????",



      isEnabled: true,



      repeatMode: "Analog",



      triggerBehavior: "Hold",



      trigger: { control: "LeftStickY", mode: "AxisPositive", threshold: 0.01 },



      action: { type: "MouseMove", parameter: "Y", sensitivity: -1 }



    })



  );







  verticalRule.displayName = "左摇杆纵向";



  verticalRule.isEnabled = true;



  verticalRule.repeatMode = "Analog";



  verticalRule.triggerBehavior = "Hold";



  verticalRule.trigger = { control: "LeftStickY", mode: "AxisPositive", threshold: 0.01 };



  verticalRule.action = { type: "MouseMove", parameter: "Y", sensitivity: -1 };







  const leftThumbRule = upsertRule(



    profile,



    (rule) => rule.trigger.control === "LeftThumb",



    () => ({



      id: randomId(),



      displayName: "?????",



      isEnabled: true,



      repeatMode: "WhileHeld",



      triggerBehavior: "Hold",



      trigger: { control: "LeftThumb", mode: "Button", threshold: 0.45 },



      action: { type: "MouseButton", parameter: "Left", sensitivity: 1 }



    })



  );







  leftThumbRule.displayName = "左摇杆按下";



  leftThumbRule.isEnabled = true;



  leftThumbRule.repeatMode = "WhileHeld";



  leftThumbRule.triggerBehavior = "Hold";



  leftThumbRule.trigger = { control: "LeftThumb", mode: "Button", threshold: 0.45 };



  leftThumbRule.action = { type: "MouseButton", parameter: "Left", sensitivity: 1 };







  state.selectedRuleId = leftThumbRule.id;



  markDirty();



  renderAll();



  showToast("已应用桌面鼠标预设：左摇杆移动鼠标，按下左摇杆为鼠标左键。", "success");



}







function upsertRule(profile, matcher, factory) {



  const existing = profile.rules.find(matcher);



  if (existing) {



    return existing;



  }







  const created = factory();



  profile.rules.push(created);



  return created;



}







function deleteSelectedRule() {



  const profile = getProfile();



  const rule = getSelectedRule();



  if (!profile || !rule) {



    return;



  }







  if (profile.rules.length <= 1) {



    showToast("至少保留一条规则。", "error");



    return;



  }







  profile.rules = profile.rules.filter((item) => item.id !== rule.id);



  state.selectedRuleId = profile.rules[0]?.id ?? null;



  markDirty();



  renderAll();



}







function updateProfile(mutator) {



  const profile = getProfile();



  if (!profile) {



    return;



  }



  mutator(profile);



  markDirty();



}







function updateSelectedRule(mutator, rerender = false) {



  const rule = getSelectedRule();



  if (!rule) {



    return;



  }







  mutator(rule);



  if (rule.action.type === "SystemAction") {



    rule.action.parameter = normalizeSystemAction(rule.action.parameter || "VolumeUp") || "VolumeUp";



  }



  markDirty();



  syncEditor();



  renderRuleList();



  renderSummary();



  if (rerender) {



    renderCaptureState();



  }



}







function renderAll() {



  renderProfile();



  renderRuleList();



  syncEditor();



  renderSummary();



  renderDirtyState();



}







function renderProfile() {



  const profile = getProfile();



  if (!profile) {



    return;



  }







  refs.profileTitle.textContent = profile.name || "配置 1";



  refs.profileNameInput.value = profile.name || "配置 1";



  refs.controllerSlotSelect.value = String(profile.controllerSlot ?? 0);



  refs.runtimeEnabledToggle.checked = !!state.currentConfig.runtimeEnabled;



  refs.autostartToggle.checked = !!state.currentConfig.startWithWindows;



  refs.baseSpeedInput.value = profile.cursorSettings.baseSpeed ?? 24;



  refs.accelerationInput.value = profile.cursorSettings.acceleration ?? 1.35;



  refs.scrollStepInput.value = profile.cursorSettings.scrollStep ?? 120;



  refs.deadZoneInput.value = profile.cursorSettings.deadZone ?? 0.18;



}







function renderRuleList() {



  const profile = getProfile();



  if (!profile) {



    refs.ruleList.innerHTML = "";



    return;



  }







  refs.ruleList.innerHTML = profile.rules.map((rule) => {



    const selected = rule.id === state.selectedRuleId ? "is-selected" : "";



    const disabled = rule.isEnabled ? "" : "is-disabled";



    return `



      <button type="button" class="rule-item ${selected} ${disabled}" data-rule-id="${rule.id}">



        <span class="rule-name">${escapeHtml(rule.displayName || readableControl(rule.trigger.control, rule.trigger.mode))}</span>



        <span class="rule-meta">${escapeHtml(readableControl(rule.trigger.control, rule.trigger.mode))} -> ${escapeHtml(describeAction(rule))}</span>



      </button>`;



  }).join("");







  refs.ruleList.querySelectorAll(".rule-item").forEach((button) => {



    button.addEventListener("click", () => {



      state.selectedRuleId = button.dataset.ruleId;



      syncEditor();



      renderRuleList();



    });



  });



}







function syncEditor() {



  const rule = getSelectedRule();



  if (!rule) {



    refs.editorHint.textContent = "选择规则后开始编辑";



    return;



  }







  refs.editorHint.textContent = `当前编辑：${rule.displayName || readableControl(rule.trigger.control, rule.trigger.mode)}`;



  refs.displayNameInput.value = rule.displayName || "";



  refs.controlSelect.value = rule.trigger.control;



  refs.triggerModeSelect.value = rule.trigger.mode;



  refs.repeatModeSelect.value = rule.repeatMode;



  refs.actionTypeSelect.value = rule.action.type;



  refs.thresholdInput.value = rule.trigger.threshold ?? 0.45;



  refs.parameterInput.value = rule.action.parameter ?? "";



  refs.sensitivityInput.value = rule.action.sensitivity ?? 1;



  refs.ruleEnabledToggle.checked = !!rule.isEnabled;



  refs.behaviorDescription.textContent = BEHAVIOR_DETAILS[rule.triggerBehavior] || "";







  document.querySelectorAll(".trigger-palette button").forEach((button) => {



    button.classList.toggle("is-active", button.dataset.behavior === rule.triggerBehavior);



  });







  refs.systemActionSelect.value = normalizeSystemAction(rule.action.parameter) || "VolumeUp";



  renderActionEditor(rule);



}







function renderActionEditor(rule) {



  const isSystemAction = rule.action.type === "SystemAction";



  refs.systemActionRow.classList.toggle("hidden", !isSystemAction);



  refs.parameterRow.classList.toggle("hidden", isSystemAction);



  refs.captureStrip.classList.toggle("hidden", isSystemAction);



  refs.captureStrip.classList.toggle("is-disabled", isSystemAction);







  if (isSystemAction) {



    refs.captureHint.textContent = "系统动作不需要录制，请直接从下拉列表选择。";



  } else if (state.capture.active) {



    refs.captureHint.textContent = "录制中：现在可以按键盘组合键、点鼠标按钮或滚动滚轮。应用会拦截 Ctrl+F4，避免网页被直接关闭。";



  } else {



    refs.captureHint.textContent = "点击“开始录制”后，可直接按键盘单键、组合键、鼠标左中右键，或滚动鼠标滚轮。录制期间网页不会抢先处理 Ctrl+F4 这类组合键。";



  }



}







function renderSummary() {



  const profile = getProfile();



  if (!profile) {



    refs.mappingSummary.innerHTML = "";



    return;



  }







  const importantControls = new Set(["A", "B", "X", "Y", "DPadUp", "DPadDown", "DPadLeft", "DPadRight", "RightStickY"]);



  const summaryRules = profile.rules.filter((rule) => importantControls.has(rule.trigger.control));







  refs.mappingSummary.innerHTML = summaryRules.map((rule) => `



    <article class="summary-item">



      <div>



        <p class="summary-name">${escapeHtml(rule.displayName || readableControl(rule.trigger.control, rule.trigger.mode))}</p>



        <p class="summary-meta">${escapeHtml(BEHAVIOR_LABELS[rule.triggerBehavior] || rule.triggerBehavior)} / ${escapeHtml(readableControl(rule.trigger.control, rule.trigger.mode))}</p>



      </div>



      <strong>${escapeHtml(describeAction(rule))}</strong>



    </article>



  `).join("");



}







function renderDirtyState() {



  refs.dirtyState.textContent = state.dirty ? "待保存" : "未修改";



  refs.dirtyState.className = state.dirty ? "status-chip status-chip-warn" : "status-chip status-chip-muted";



}







function markDirty() {



  state.dirty = true;



  renderDirtyState();



}







function getProfile() {



  return state.currentConfig?.profile ?? null;



}







function getCursorSettings() {



  return getProfile()?.cursorSettings ?? null;



}







function getSelectedRule() {



  const rules = getProfile()?.rules ?? [];



  return rules.find((rule) => rule.id === state.selectedRuleId) ?? null;



}







function createRule() {



  return {



    id: randomId(),



    displayName: "新规则",



    isEnabled: true,



    repeatMode: "OnPress",



    triggerBehavior: "SinglePress",



    trigger: { control: "A", mode: "Button", threshold: 0.45 },



    action: { type: "KeyboardKey", parameter: "SPACE", sensitivity: 1 }



  };



}







function readableControl(control, mode) {



  const label = CONTROL_OPTIONS.find(([value]) => value === control)?.[1] || control;



  if (control === "RightStickY" && mode === "AxisPositive") {



    return "右摇杆上";



  }



  if (control === "RightStickY" && mode === "AxisNegative") {



    return "右摇杆下";



  }



  return label;



}







function describeAction(rule) {



  if (rule.action.type === "SystemAction") {



    return SYSTEM_ACTION_OPTIONS.find(([value]) => value === normalizeSystemAction(rule.action.parameter))?.[1] || rule.action.parameter || "系统动作";



  }



  if (rule.action.type === "MouseWheel") {



    return Number(rule.action.sensitivity ?? 1) >= 0 ? "鼠标滚轮上滚" : "鼠标滚轮下滚";



  }



  return rule.action.parameter || ACTION_TYPE_OPTIONS.find(([value]) => value === rule.action.type)?.[1] || rule.action.type;



}







function looksAutoNamed(value) {



  return !value || value === "新规则" || value.includes("摇杆") || value.includes("方向键") || value.endsWith("键") || value === "LB" || value === "RB" || value === "LT" || value === "RT";



}







function safeNumber(value, fallback) {



  const parsed = Number(value);



  return Number.isFinite(parsed) ? parsed : fallback;



}







function escapeHtml(value) {



  return String(value ?? "")



    .replace(/&/g, "&amp;")



    .replace(/</g, "&lt;")



    .replace(/>/g, "&gt;")



    .replace(/"/g, "&quot;")



    .replace(/'/g, "&#39;");



}







function showToast(message, tone = "success") {



  refs.toast.textContent = message;



  refs.toast.className = `toast is-visible tone-${tone}`;



  window.clearTimeout(toastTimer);



  toastTimer = window.setTimeout(() => {



    refs.toast.className = "toast";



  }, 2400);



}







function clearCapturedParameter() {



  updateSelectedRule((rule) => {



    if (rule.action.type === "SystemAction") {



      rule.action.parameter = "VolumeUp";



    } else {



      rule.action.parameter = "";



    }



  }, true);



}







async function toggleCapture() {



  const rule = getSelectedRule();



  if (!rule) {



    showToast("请先选择一条规则。", "error");



    return;



  }







  if (rule.action.type === "SystemAction") {



    showToast("系统动作请直接从下拉列表选择。", "error");



    return;



  }







  if (state.capture.active) {



    await cancelNativeCapture("已取消录制。", false);



    return;



  }







  try {



    const response = await fetch("/api/capture/start", { method: "POST" });



    if (!response.ok) {



      throw new Error("无法开始录制");



    }







    state.capture.active = true;



    renderCaptureState();



    pollNativeCapture();



  } catch (error) {



    showToast(error.message || "无法开始录制。", "error");



  }



}







function renderCaptureState() {



  refs.captureButton.textContent = state.capture.active ? "取消录制" : "开始录制";



  refs.captureButton.classList.toggle("is-recording", state.capture.active);



  const rule = getSelectedRule();



  if (rule) {



    renderActionEditor(rule);



  }



}







function pollNativeCapture() {



  window.clearTimeout(state.capture.pollTimer);



  if (!state.capture.active) {



    return;



  }







  state.capture.pollTimer = window.setTimeout(async () => {



    try {



      const response = await fetch("/api/capture", { cache: "no-store" });



      if (!response.ok) {



        throw new Error("录制状态读取失败");



      }







      const snapshot = await response.json();



      if (snapshot.completed) {



        applyCapturedSnapshot(snapshot);



        state.capture.active = false;



        renderCaptureState();



        return;



      }







      if (snapshot.active) {



        pollNativeCapture();



        return;



      }







      state.capture.active = false;



      renderCaptureState();



    } catch (error) {



      state.capture.active = false;



      renderCaptureState();



      showToast(error.message || "录制失败。", "error");



    }



  }, 140);



}







async function cancelNativeCapture(message, toast = true) {



  window.clearTimeout(state.capture.pollTimer);



  state.capture.active = false;



  renderCaptureState();



  try {



    await fetch("/api/capture/cancel", { method: "POST" });



  } catch {



  }



  if (toast) {



    showToast(message, "success");



  }



}







function applyCapturedSnapshot(snapshot) {



  updateSelectedRule((rule) => {



    if (snapshot.inputKind === "keyboard") {



      rule.action.type = snapshot.parameter && snapshot.parameter.includes("+") ? "KeyboardChord" : "KeyboardKey";



      rule.action.parameter = snapshot.parameter || "";



    } else if (snapshot.inputKind === "mouseButton") {



      rule.action.type = "MouseButton";



      rule.action.parameter = snapshot.parameter || "Left";



    } else if (snapshot.inputKind === "mouseWheel") {



      rule.action.type = "MouseWheel";



      rule.action.parameter = snapshot.parameter || "Vertical";



      rule.action.sensitivity = Number(snapshot.direction ?? 1) >= 0 ? 1 : -1;



    }



  }, true);







  syncEditor();



  renderRuleList();



  renderSummary();



  showToast(`已录制：${snapshot.displayText || snapshot.parameter || "输入"}`, "success");



}







function normalizeConfiguration(raw) {



  const profileRaw = pick(raw, "profile", "Profile") || {};



  const cursorRaw = pick(profileRaw, "cursorSettings", "CursorSettings") || {};



  const rulesRaw = pick(profileRaw, "rules", "Rules") || [];







  return {



    startWithWindows: !!pick(raw, "startWithWindows", "StartWithWindows"),



    startHidden: pick(raw, "startHidden", "StartHidden") ?? true,



    closeToTray: pick(raw, "closeToTray", "CloseToTray") ?? true,



    runtimeEnabled: pick(raw, "runtimeEnabled", "RuntimeEnabled") ?? true,



    profile: {



      id: pick(profileRaw, "id", "Id") || randomId(),



      name: pick(profileRaw, "name", "Name") || "配置 1",



      enabled: pick(profileRaw, "enabled", "Enabled") ?? true,



      controllerSlot: Number(pick(profileRaw, "controllerSlot", "ControllerSlot") ?? 0),



      cursorSettings: {



        baseSpeed: safeNumber(pick(cursorRaw, "baseSpeed", "BaseSpeed"), 24),



        acceleration: safeNumber(pick(cursorRaw, "acceleration", "Acceleration"), 1.35),



        scrollStep: safeNumber(pick(cursorRaw, "scrollStep", "ScrollStep"), 120),



        deadZone: safeNumber(pick(cursorRaw, "deadZone", "DeadZone"), 0.18)



      },



      rules: (Array.isArray(rulesRaw) ? rulesRaw : []).map(normalizeRule)



    }



  };



}







function normalizeRule(raw) {



  const triggerRaw = pick(raw, "trigger", "Trigger") || {};



  const actionRaw = pick(raw, "action", "Action") || {};



  const actionType = pick(actionRaw, "type", "Type") || "KeyboardKey";



  let parameter = pick(actionRaw, "parameter", "Parameter") || "";







  if (actionType === "SystemAction") {



    parameter = normalizeSystemAction(parameter) || "VolumeUp";



  }







  return {



    id: pick(raw, "id", "Id") || randomId(),



    displayName: pick(raw, "displayName", "DisplayName") || readableControl(pick(triggerRaw, "control", "Control") || "A", pick(triggerRaw, "mode", "Mode") || "Button"),



    isEnabled: pick(raw, "isEnabled", "IsEnabled") ?? true,



    repeatMode: pick(raw, "repeatMode", "RepeatMode") || "OnPress",



    triggerBehavior: pick(raw, "triggerBehavior", "TriggerBehavior") || "SinglePress",



    trigger: {



      control: pick(triggerRaw, "control", "Control") || "A",



      mode: pick(triggerRaw, "mode", "Mode") || "Button",



      threshold: safeNumber(pick(triggerRaw, "threshold", "Threshold"), 0.45)



    },



    action: {



      type: actionType,



      parameter,



      sensitivity: safeNumber(pick(actionRaw, "sensitivity", "Sensitivity"), 1)



    }



  };



}







function normalizeSystemAction(value) {



  if (!value) {



    return "";



  }



  return SYSTEM_ACTION_CANONICAL[value] || SYSTEM_ACTION_CANONICAL[String(value).trim()] || String(value).trim();



}







function pick(obj, ...keys) {



  for (const key of keys) {



    if (obj && Object.prototype.hasOwnProperty.call(obj, key)) {



      return obj[key];



    }



  }



  return undefined;



}







function randomId() {



  return crypto.randomUUID ? crypto.randomUUID().replace(/-/g, "") : `${Date.now()}${Math.random().toString(16).slice(2)}`;



}



