const fileInput = document.querySelector("#fileInput");
const pickFiles = document.querySelector("#pickFiles");
const pickFolder = document.querySelector("#pickFolder");
const dropZone = document.querySelector("#dropZone");
const scaleRange = document.querySelector("#scaleRange");
const scaleValue = document.querySelector("#scaleValue");
const maxWidthInput = document.querySelector("#maxWidth");
const maxHeightInput = document.querySelector("#maxHeight");
const optimizeMode = document.querySelector("#optimizeMode");
const outputFormat = document.querySelector("#outputFormat");
const keepSmaller = document.querySelector("#keepSmaller");
const compressButton = document.querySelector("#compressButton");
const clearButton = document.querySelector("#clearButton");
const resultsList = document.querySelector("#resultsList");
const fileCount = document.querySelector("#fileCount");
const savedTotal = document.querySelector("#savedTotal");

let files = [];
let outputDirectory = null;

const iconDownload = `
  <svg viewBox="0 0 24 24" aria-hidden="true">
    <path d="M11 4h2v9l3.3-3.3 1.4 1.4L12 16.8l-5.7-5.7 1.4-1.4L11 13V4Zm-6 14h14v2H5v-2Z"></path>
  </svg>
`;

pickFiles.addEventListener("click", () => fileInput.click());
fileInput.addEventListener("change", () => addFiles(fileInput.files));
scaleRange.addEventListener("input", () => {
  scaleValue.value = `${scaleRange.value}%`;
});
compressButton.addEventListener("click", compressAll);
clearButton.addEventListener("click", clearFiles);
pickFolder.addEventListener("click", chooseOutputDirectory);

["dragenter", "dragover"].forEach((eventName) => {
  dropZone.addEventListener(eventName, (event) => {
    event.preventDefault();
    dropZone.classList.add("dragging");
  });
});

["dragleave", "drop"].forEach((eventName) => {
  dropZone.addEventListener(eventName, (event) => {
    event.preventDefault();
    dropZone.classList.remove("dragging");
  });
});

dropZone.addEventListener("drop", (event) => {
  addFiles(event.dataTransfer.files);
});

function addFiles(fileList) {
  const nextFiles = Array.from(fileList).filter((file) => file.type === "image/png" || file.name.toLowerCase().endsWith(".png"));

  if (nextFiles.length === 0) {
    return;
  }

  files = [...files, ...nextFiles];
  fileInput.value = "";
  renderPendingFiles();
}

function clearFiles() {
  files = [];
  outputDirectory = null;
  fileInput.value = "";
  renderPendingFiles();
}

function renderPendingFiles() {
  fileCount.textContent = `${files.length} 个文件`;
  savedTotal.textContent = files.length ? "准备压缩" : "等待处理";
  compressButton.disabled = files.length === 0;
  clearButton.disabled = files.length === 0;

  if (files.length === 0) {
    resultsList.innerHTML = `<div class="empty-state">暂无 PNG 文件</div>`;
    return;
  }

  resultsList.innerHTML = "";
  files.forEach((file, index) => {
    const row = createResultRow(file, index);
    resultsList.appendChild(row);
  });
}

function createResultRow(file, index) {
  const row = document.createElement("article");
  row.className = "result-item";
  row.dataset.index = String(index);
  row.innerHTML = `
    <img class="thumb" alt="" src="${URL.createObjectURL(file)}">
    <div class="item-main">
      <div class="file-name" title="${escapeHtml(file.name)}">${escapeHtml(file.name)}</div>
      <div class="meta">
        <span>${formatBytes(file.size)}</span>
        <span>等待</span>
      </div>
      <div class="status">未处理</div>
    </div>
    <span></span>
  `;
  return row;
}

async function chooseOutputDirectory() {
  if (!("showDirectoryPicker" in window)) {
    savedTotal.textContent = "浏览器下载";
    return;
  }

  try {
    outputDirectory = await window.showDirectoryPicker({ mode: "readwrite" });
    savedTotal.textContent = "已选择文件夹";
  } catch (error) {
    if (error.name !== "AbortError") {
      savedTotal.textContent = "文件夹不可用";
    }
  }
}

async function compressAll() {
  if (files.length === 0) {
    return;
  }

  setRunning(true);
  let originalTotal = 0;
  let outputTotal = 0;

  for (let index = 0; index < files.length; index += 1) {
    const file = files[index];
    originalTotal += file.size;
    updateRow(index, { status: "处理中", meta: `${formatBytes(file.size)} -> ...` });

    try {
      const result = await compressImage(file);
      const shouldUseOriginal = keepSmaller.checked && result.blob.size >= file.size;
      const finalBlob = shouldUseOriginal ? file : result.blob;
      outputTotal += finalBlob.size;

      if (outputDirectory && !shouldUseOriginal) {
        await saveToDirectory(finalBlob, result.outputName);
      }

      updateRow(index, {
        status: shouldUseOriginal ? "已跳过，结果未变小" : "完成",
        meta: `${result.inputWidth}x${result.inputHeight} -> ${result.outputWidth}x${result.outputHeight}`,
        detail: `${formatBytes(file.size)} -> ${formatBytes(finalBlob.size)}，${formatRatio(file.size, finalBlob.size)}`,
        blob: outputDirectory || shouldUseOriginal ? null : finalBlob,
        outputName: result.outputName,
        warn: shouldUseOriginal
      });
    } catch (error) {
      outputTotal += file.size;
      updateRow(index, {
        status: "失败",
        meta: formatBytes(file.size),
        detail: error.message || "无法处理该图片",
        error: true
      });
    }
  }

  savedTotal.textContent = formatRatio(originalTotal, outputTotal);
  setRunning(false);
}

async function compressImage(file) {
  const image = await loadImage(file);
  const target = getTargetSize(image.naturalWidth, image.naturalHeight);
  const canvas = document.createElement("canvas");
  const ctx = canvas.getContext("2d", { willReadFrequently: true });

  canvas.width = target.width;
  canvas.height = target.height;
  ctx.imageSmoothingEnabled = true;
  ctx.imageSmoothingQuality = "high";
  ctx.clearRect(0, 0, canvas.width, canvas.height);
  ctx.drawImage(image, 0, 0, target.width, target.height);

  if (optimizeMode.value !== "reencode") {
    applyColorOptimization(ctx, canvas.width, canvas.height, getSelectedColorCount());
  }

  if (optimizeMode.value === "strong") {
    trimTransparentRgb(ctx, canvas.width, canvas.height);
  }

  const format = outputFormat.value;
  const blob = await canvasToBlob(canvas, format);
  URL.revokeObjectURL(image.src);

  return {
    blob,
    inputWidth: image.naturalWidth,
    inputHeight: image.naturalHeight,
    outputWidth: target.width,
    outputHeight: target.height,
    outputName: buildOutputName(file.name, format)
  };
}

function getTargetSize(width, height) {
  const scale = Number(scaleRange.value) / 100;
  const maxWidth = Number(maxWidthInput.value) || Infinity;
  const maxHeight = Number(maxHeightInput.value) || Infinity;
  const sizeScale = Math.min(scale, maxWidth / width, maxHeight / height, 1);

  return {
    width: Math.max(1, Math.round(width * sizeScale)),
    height: Math.max(1, Math.round(height * sizeScale))
  };
}

function applyColorOptimization(ctx, width, height, colorCount) {
  const imageData = ctx.getImageData(0, 0, width, height);
  const data = imageData.data;
  const levels = Math.max(2, Math.round(Math.cbrt(colorCount)));
  const step = 255 / (levels - 1);

  for (let index = 0; index < data.length; index += 4) {
    const alpha = data[index + 3];

    if (alpha === 0) {
      data[index] = 0;
      data[index + 1] = 0;
      data[index + 2] = 0;
      continue;
    }

    data[index] = Math.round(Math.round(data[index] / step) * step);
    data[index + 1] = Math.round(Math.round(data[index + 1] / step) * step);
    data[index + 2] = Math.round(Math.round(data[index + 2] / step) * step);
  }

  ctx.putImageData(imageData, 0, 0);
}

function trimTransparentRgb(ctx, width, height) {
  const imageData = ctx.getImageData(0, 0, width, height);
  const data = imageData.data;

  for (let index = 0; index < data.length; index += 4) {
    if (data[index + 3] < 3) {
      data[index] = 0;
      data[index + 1] = 0;
      data[index + 2] = 0;
      data[index + 3] = 0;
    }
  }

  ctx.putImageData(imageData, 0, 0);
}

function loadImage(file) {
  return new Promise((resolve, reject) => {
    const image = new Image();
    const url = URL.createObjectURL(file);

    image.onload = () => resolve(image);
    image.onerror = () => {
      URL.revokeObjectURL(url);
      reject(new Error("图片读取失败"));
    };
    image.src = url;
  });
}

function canvasToBlob(canvas, format) {
  const mimeType = format === "webp" ? "image/webp" : "image/png";
  const quality = format === "webp" ? 0.88 : undefined;

  return new Promise((resolve, reject) => {
    canvas.toBlob((blob) => {
      if (blob) {
        resolve(blob);
      } else {
        reject(new Error(`${format.toUpperCase()} 编码失败`));
      }
    }, mimeType, quality);
  });
}

async function saveToDirectory(blob, outputName) {
  const handle = await outputDirectory.getFileHandle(outputName, { create: true });
  const writable = await handle.createWritable();
  await writable.write(blob);
  await writable.close();
}

function updateRow(index, data) {
  const row = resultsList.querySelector(`[data-index="${index}"]`);
  if (!row) {
    return;
  }

  const meta = row.querySelector(".meta");
  const status = row.querySelector(".status");
  const action = row.lastElementChild;

  meta.innerHTML = `<span>${escapeHtml(data.meta || "")}</span>`;
  status.innerHTML = `
    <span class="${data.error ? "error" : data.warn ? "warn" : ""}">${escapeHtml(data.status || "")}</span>
    ${data.detail ? `<strong>${escapeHtml(data.detail)}</strong>` : ""}
  `;
  action.innerHTML = "";

  if (data.blob) {
    const link = document.createElement("a");
    link.className = "download-link";
    link.href = URL.createObjectURL(data.blob);
    link.download = data.outputName;
    link.title = "下载";
    link.setAttribute("aria-label", "下载");
    link.innerHTML = iconDownload;
    action.appendChild(link);
  }
}

function setRunning(isRunning) {
  compressButton.disabled = isRunning || files.length === 0;
  pickFiles.disabled = isRunning;
  clearButton.disabled = isRunning || files.length === 0;
  compressButton.querySelector("span").textContent = isRunning ? "压缩中" : "开始压缩";
}

function getSelectedColorCount() {
  const checked = document.querySelector("input[name='colors']:checked");
  return Number(checked?.value || 256);
}

function buildOutputName(name, format) {
  const baseName = name.replace(/\.png$/i, "");
  return `${baseName}.compressed.${format}`;
}

function formatBytes(bytes) {
  if (bytes < 1024) {
    return `${bytes} B`;
  }

  const units = ["KB", "MB", "GB"];
  let value = bytes / 1024;
  let unitIndex = 0;

  while (value >= 1024 && unitIndex < units.length - 1) {
    value /= 1024;
    unitIndex += 1;
  }

  return `${value.toFixed(value >= 10 ? 1 : 2)} ${units[unitIndex]}`;
}

function formatRatio(original, output) {
  if (!original || !output) {
    return "节省 0%";
  }

  const saved = Math.max(0, 1 - output / original);
  return `节省 ${(saved * 100).toFixed(1)}%`;
}

function escapeHtml(value) {
  return String(value)
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#039;");
}
