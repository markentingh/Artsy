import React, { useRef, useEffect } from 'react';

const vertexShaderSrc = `
  precision mediump float;
  attribute vec2 aPos;
  attribute vec2 aUv;
  varying vec2 vUv;

  void main() {
    vUv = aUv;
    gl_Position = vec4(aPos, 0.0, 1.0);
  }
`;

const fragmentShaderSrc = `
  precision mediump float;
  uniform sampler2D imageTexture;
  uniform float time;
  uniform vec2 resolution;
  uniform float zoom;
  uniform vec2 target;
  varying vec2 vUv;

  vec3 permute(vec3 x) { return mod(((x*34.0)+1.0)*x, 289.0); }

  float snoise(vec2 v) {
    const vec4 C = vec4(0.211324865405187, 0.366025403784439, -0.577350269189626, 0.024390243902439);
    vec2 i  = floor(v + dot(v, C.yy));
    vec2 x0 = v - i + dot(i, C.xx);
    vec2 i1 = (x0.x > x0.y) ? vec2(1.0, 0.0) : vec2(0.0, 1.0);
    vec4 x12 = x0.xyxy + C.xxzz;
    x12.xy -= i1;
    i = mod(i, 289.0);
    vec3 p = permute(permute(i.y + vec3(0.0, i1.y, 1.0)) + i.x + vec3(0.0, i1.x, 1.0));
    vec3 m = max(0.5 - vec3(dot(x0,x0), dot(x12.xy,x12.xy), dot(x12.zw,x12.zw)), 0.0);
    m = m * m;
    m = m * m;
    vec3 x = 2.0 * fract(p * C.www) - 1.0;
    vec3 h = abs(x) - 0.5;
    vec3 ox = floor(x + 0.5);
    vec3 a0 = x - ox;
    m *= 1.79284291400159 - 0.85373472095314 * (a0 * a0 + h * h);
    vec3 g;
    g.x  = a0.x  * x0.x  + h.x  * x0.y;
    g.yz = a0.yz * x12.xz + h.yz * x12.yw;
    return 130.0 * dot(m, g);
  }

  void main() {
    vec2 uv = target + (vUv - 0.5) / zoom;

    vec4 imgS = texture2D(imageTexture, uv);
    vec3 img = imgS.rgb;

    float n = snoise(uv * 4.0 + vec2(time * 0.3, 0.0));
    float nPos = max(n, 0.0);

    vec2 pixel = 1.0 / (resolution * zoom);
    vec3 bloom = vec3(0.0);

    for (int x = -1; x <= 1; x++) {
      for (int y = -1; y <= 1; y++) {
        vec2 off = vec2(float(x), float(y)) * pixel;
        vec3 s = texture2D(imageTexture, uv + off).rgb;
        float b = dot(s, vec3(0.299, 0.587, 0.114));
        float mask = smoothstep(0.2, 0.6, b);
        int d = (x < 0 ? -x : x) + (y < 0 ? -y : y);
        float weight;
        if (d == 0) weight = 0.25;
        else if (d == 1) weight = 0.125;
        else weight = 0.0625;
        bloom += s * mask * weight;
      }
    }

    vec3 final = img + bloom * nPos * 4.0;
    gl_FragColor = vec4(final, imgS.a);
  }
`;

function compileShader(gl, type, src) {
  const shader = gl.createShader(type);
  gl.shaderSource(shader, src);
  gl.compileShader(shader);
  if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
    console.error(gl.getShaderInfoLog(shader));
    gl.deleteShader(shader);
    return null;
  }
  return shader;
}

function createProgram(gl, vs, fs) {
  const program = gl.createProgram();
  gl.attachShader(program, vs);
  gl.attachShader(program, fs);
  gl.linkProgram(program);
  if (!gl.getProgramParameter(program, gl.LINK_STATUS)) {
    console.error(gl.getProgramInfoLog(program));
    gl.deleteProgram(program);
    return null;
  }
  return program;
}

function clamp(v, min, max) {
  return Math.max(min, Math.min(max, v));
}

export default function ShinyImage({ src, alt, className }) {
  const containerRef = useRef(null);

  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;

    let cancelled = false;
    let rafId;
    let cleanup = () => {};

    const img = new Image();
    img.onload = () => {
      if (cancelled) return;

      const offscreenCanvas = document.createElement('canvas');
      offscreenCanvas.width = img.width;
      offscreenCanvas.height = img.height;
      const gl = offscreenCanvas.getContext('webgl', { alpha: true, premultipliedAlpha: false, preserveDrawingBuffer: true });
      if (!gl) return;

      const vs = compileShader(gl, gl.VERTEX_SHADER, vertexShaderSrc);
      const fs = compileShader(gl, gl.FRAGMENT_SHADER, fragmentShaderSrc);
      if (!vs || !fs) return;

      const program = createProgram(gl, vs, fs);
      if (!program) return;
      gl.useProgram(program);

      const posLoc = gl.getAttribLocation(program, 'aPos');
      const uvLoc = gl.getAttribLocation(program, 'aUv');
      const imageTexLoc = gl.getUniformLocation(program, 'imageTexture');
      const timeLoc = gl.getUniformLocation(program, 'time');
      const resolutionLoc = gl.getUniformLocation(program, 'resolution');
      const zoomLoc = gl.getUniformLocation(program, 'zoom');
      const targetLoc = gl.getUniformLocation(program, 'target');

      const vertices = new Float32Array([
        -1, -1, 0, 0,
         1, -1, 1, 0,
        -1,  1, 0, 1,
        -1,  1, 0, 1,
         1, -1, 1, 0,
         1,  1, 1, 1,
      ]);

      const buffer = gl.createBuffer();
      gl.bindBuffer(gl.ARRAY_BUFFER, buffer);
      gl.bufferData(gl.ARRAY_BUFFER, vertices, gl.STATIC_DRAW);

      gl.enableVertexAttribArray(posLoc);
      gl.vertexAttribPointer(posLoc, 2, gl.FLOAT, false, 16, 0);
      gl.enableVertexAttribArray(uvLoc);
      gl.vertexAttribPointer(uvLoc, 2, gl.FLOAT, false, 16, 8);

      const texture = gl.createTexture();
      gl.activeTexture(gl.TEXTURE0);
      gl.bindTexture(gl.TEXTURE_2D, texture);
      gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, 1);
      gl.pixelStorei(gl.UNPACK_COLORSPACE_CONVERSION_WEBGL, gl.NONE);
      gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, img);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
      gl.uniform1i(imageTexLoc, 0);

      const displayCanvas = document.createElement('canvas');
      const displayCtx = displayCanvas.getContext('2d');

      const setDisplaySize = () => {
        const w = container.clientWidth;
        const h = w / (img.width / img.height);
        if (w === 0 || h === 0) return false;
        const dpr = Math.min(window.devicePixelRatio, 2);
        displayCanvas.width = Math.floor(w * dpr);
        displayCanvas.height = Math.floor(h * dpr);
        displayCanvas.style.width = `${w}px`;
        displayCanvas.style.height = `${h}px`;
        displayCtx.imageSmoothingEnabled = true;
        displayCtx.imageSmoothingQuality = 'high';
        return true;
      };

      if (!setDisplaySize()) return;
      container.appendChild(displayCanvas);

      const targetTarget = { x: 0.5, y: 0.5 };
      let targetZoom = 1.0;
      const current = { x: 0.5, y: 0.5, zoom: 1.0, time: 0 };

      const handleMove = (e) => {
        const rect = displayCanvas.getBoundingClientRect();
        const x = (e.clientX - rect.left) / rect.width;
        const y = 1.0 - (e.clientY - rect.top) / rect.height;
        targetTarget.x = clamp(x, 1.0 / 3.0, 2.0 / 3.0);
        targetTarget.y = clamp(y, 1.0 / 3.0, 2.0 / 3.0);
        targetZoom = 1.5;
      };

      const handleEnter = () => { targetZoom = 1.5; };
      const handleLeave = () => { targetZoom = 1.0; targetTarget.x = 0.5; targetTarget.y = 0.5; };

      displayCanvas.addEventListener('mousemove', handleMove);
      displayCanvas.addEventListener('mouseenter', handleEnter);
      displayCanvas.addEventListener('mouseleave', handleLeave);

      const observer = new ResizeObserver(() => {
        if (setDisplaySize()) {
          gl.viewport(0, 0, img.width, img.height);
          gl.drawArrays(gl.TRIANGLES, 0, 6);
          displayCtx.drawImage(offscreenCanvas, 0, 0, displayCanvas.width, displayCanvas.height);
        }
      });
      observer.observe(container);

      const animate = () => {
        current.time += 0.015;
        current.x += (targetTarget.x - current.x) * 0.12;
        current.y += (targetTarget.y - current.y) * 0.12;
        current.zoom += (targetZoom - current.zoom) * 0.1;

        gl.viewport(0, 0, img.width, img.height);
        gl.clearColor(0, 0, 0, 0);
        gl.clear(gl.COLOR_BUFFER_BIT);

        gl.uniform1f(timeLoc, current.time);
        gl.uniform2f(resolutionLoc, img.width, img.height);
        gl.uniform1f(zoomLoc, current.zoom);
        gl.uniform2f(targetLoc, current.x, current.y);
        gl.drawArrays(gl.TRIANGLES, 0, 6);

        displayCtx.drawImage(offscreenCanvas, 0, 0, displayCanvas.width, displayCanvas.height);
        rafId = requestAnimationFrame(animate);
      };
      rafId = requestAnimationFrame(animate);

      cleanup = () => {
        displayCanvas.removeEventListener('mousemove', handleMove);
        displayCanvas.removeEventListener('mouseenter', handleEnter);
        displayCanvas.removeEventListener('mouseleave', handleLeave);
        observer.disconnect();
        cancelAnimationFrame(rafId);
        gl.deleteProgram(program);
        gl.deleteShader(vs);
        gl.deleteShader(fs);
        gl.deleteBuffer(buffer);
        gl.deleteTexture(texture);
        if (displayCanvas.parentNode === container) {
          container.removeChild(displayCanvas);
        }
      };
    };

    img.src = src;

    return () => {
      cancelled = true;
      cleanup();
    };
  }, [src]);

  return <div ref={containerRef} className={className} role="img" aria-label={alt} />;
}
