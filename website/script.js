/* ============================================================
   ObbyistMacro — website scripts
   ============================================================ */

const REPO = "orbitthegreatest/ObbyistMacro";
const REPO_URL = `https://github.com/${REPO}`;
const RELEASES_URL = `${REPO_URL}/releases`;

const state = {
  version: null,
  assetUrl: null,
};

/* ---------------- latest release (auto-updating download) ---------------- */

async function loadLatestRelease() {
  const els = {
    hero: document.getElementById("download-hero"),
    heroLabel: document.getElementById("download-hero-label"),
    main: document.getElementById("download-main"),
    mainLabel: document.getElementById("download-main-label"),
    info: document.getElementById("release-info-text"),
    info2: document.getElementById("release-info-2-text"),
  };
  const spinners = document.querySelectorAll(".spinner");

  try {
    const res = await fetch(
      `https://api.github.com/repos/${REPO}/releases/latest`,
      { headers: { Accept: "application/vnd.github+json" } }
    );
    if (!res.ok) throw new Error(`GitHub API ${res.status}`);

    const release = await res.json();
    const asset = pickAsset(release.assets);

    if (asset) {
      state.version = release.tag_name;
      state.assetUrl = asset.browser_download_url;
      els.hero.href = state.assetUrl;
      els.main.href = state.assetUrl;
      els.heroLabel.textContent = `Download v${release.tag_name.replace(/^v/i, "")}`;
      els.mainLabel.textContent = "Download the installer (.exe)";
    } else {
      // Release exists but has no .exe attached yet -> point to the releases page.
      els.hero.href = RELEASES_URL;
      els.main.href = RELEASES_URL;
      els.heroLabel.textContent = "Download for Windows";
      els.mainLabel.textContent = "No installer found — see releases";
    }

    const date = formatDate(release.published_at);
    els.info.textContent = `Latest: ${release.tag_name} — ${release.name || ""} • ${date}`.replace(/\s+/g, " ").trim();
    els.info2.textContent = `${release.tag_name} • ${release.name || ""} • ${date}`.replace(/\s+/g, " ").trim();
  } catch {
    // No release yet (or offline) -> fall back to the releases page.
    els.hero.href = RELEASES_URL;
    els.main.href = RELEASES_URL;
    els.heroLabel.textContent = "Download for Windows";
    els.mainLabel.textContent = "Download for Windows";
    els.info.textContent = "No release published yet — check back soon!";
    els.info2.textContent = "No release published yet — check back soon!";
  } finally {
    spinners.forEach((s) => (s.style.display = "none"));
  }
}

function pickAsset(assets = []) {
  const exes = assets.filter((a) => /\.exe$/i.test(a.name));
  if (exes.length === 0) return null;
  return (
    exes.find((a) => /setup/i.test(a.name)) ||
    exes.find((a) => /install/i.test(a.name)) ||
    exes[0]
  );
}

function formatDate(iso) {
  if (!iso) return "recently";
  try {
    return new Date(iso).toLocaleDateString(undefined, {
      year: "numeric",
      month: "short",
      day: "numeric",
    });
  } catch {
    return "recently";
  }
}

/* ---------------- particle field ---------------- */

const canvas = document.getElementById("particles");
const ctx = canvas.getContext("2d");
let particles = [];
let rafId = null;

function resizeCanvas() {
  canvas.width = window.innerWidth;
  canvas.height = window.innerHeight;
}

function spawnParticles() {
  const count = Math.min(70, Math.floor(canvas.width / 22));
  particles = Array.from({ length: count }, () => ({
    x: Math.random() * canvas.width,
    y: Math.random() * canvas.height,
    r: Math.random() * 1.8 + 0.6,
    vx: (Math.random() - 0.5) * 0.22,
    vy: (Math.random() - 0.5) * 0.22,
    a: Math.random() * 0.5 + 0.12,
  }));
}

function drawParticles(t) {
  ctx.clearRect(0, 0, canvas.width, canvas.height);

  for (const p of particles) {
    p.x += p.vx;
    p.y += p.vy;
    if (p.x < 0 || p.x > canvas.width) p.vx *= -1;
    if (p.y < 0 || p.y > canvas.height) p.vy *= -1;

    const twinkle = 0.7 + Math.sin(t / 600 + p.x * 0.01 + p.y * 0.01) * 0.3;
    ctx.beginPath();
    ctx.arc(p.x, p.y, p.r, 0, Math.PI * 2);
    ctx.fillStyle = `rgba(59, 255, 136, ${p.a * twinkle})`;
    ctx.fill();
  }

  for (let i = 0; i < particles.length; i++) {
    for (let j = i + 1; j < particles.length; j++) {
      const dx = particles[i].x - particles[j].x;
      const dy = particles[i].y - particles[j].y;
      const d = dx * dx + dy * dy;
      if (d < 110 * 110) {
        ctx.beginPath();
        ctx.moveTo(particles[i].x, particles[i].y);
        ctx.lineTo(particles[j].x, particles[j].y);
        ctx.strokeStyle = `rgba(59, 255, 136, ${0.08 * (1 - d / (110 * 110))})`;
        ctx.lineWidth = 1;
        ctx.stroke();
      }
    }
  }

  rafId = requestAnimationFrame(drawParticles);
}

/* ---------------- reveal on scroll ---------------- */

function setupReveal() {
  const io = new IntersectionObserver(
    (entries) => {
      for (const entry of entries) {
        if (entry.isIntersecting) {
          entry.target.classList.add("visible");
          io.unobserve(entry.target);
        }
      }
    },
    { threshold: 0.12 }
  );
  document.querySelectorAll(".reveal").forEach((el) => io.observe(el));
}

/* ---------------- init ---------------- */

const prefersReduced = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

resizeCanvas();
window.addEventListener("resize", () => {
  resizeCanvas();
  spawnParticles();
});

if (prefersReduced) {
  canvas.style.display = "none";
} else {
  spawnParticles();
  requestAnimationFrame(drawParticles);
}

setupReveal();
loadLatestRelease();
