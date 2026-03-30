let engine = null;
let streamAbortController = null;
let escKeyHandler = null;

export async function isWebGpuSupported() {
    if (!navigator.gpu) return false;
    try {
        const adapter = await navigator.gpu.requestAdapter();
        return adapter !== null;
    } catch {
        return false;
    }
}

export async function isModelCached(modelId) {
    try {
        const { hasModelInCache } = await import(
            "https://esm.run/@mlc-ai/web-llm"
        );
        return await hasModelInCache(modelId);
    } catch {
        return false;
    }
}

export async function initWebLlm(modelId, dotnetHelper) {
    const { CreateMLCEngine } = await import(
        "https://esm.run/@mlc-ai/web-llm"
    );

    const progressCallback = (report) => {
        dotnetHelper.invokeMethodAsync("OnModelLoadProgress", report.progress);
    };

    engine = await CreateMLCEngine(modelId, {
        initProgressCallback: progressCallback,
    });
}

export async function chatCompletionStreaming(messagesJson, dotnetHelper) {
    if (!engine) throw new Error("WebLLM engine not initialized");

    streamAbortController = new AbortController();
    const messages = JSON.parse(messagesJson);

    try {
        const stream = await engine.chat.completions.create({
            messages,
            stream: true,
            max_tokens: 512,
        });

        for await (const chunk of stream) {
            if (streamAbortController.signal.aborted) break;
            const delta = chunk.choices[0]?.delta?.content ?? "";
            if (delta) {
                await dotnetHelper.invokeMethodAsync("OnStreamChunk", delta);
            }
        }

        if (!streamAbortController.signal.aborted) {
            await dotnetHelper.invokeMethodAsync("OnStreamComplete");
        } else {
            await dotnetHelper.invokeMethodAsync("OnStreamCancelled");
        }
    } finally {
        streamAbortController = null;
    }
}

export function abortStreaming() {
    streamAbortController?.abort();
}

export function startEscapeListener(dotnetHelper) {
    stopEscapeListener();
    escKeyHandler = (e) => {
        if (e.key === 'Escape') {
            dotnetHelper.invokeMethodAsync('OnEscapePressed');
        }
    };
    document.addEventListener('keydown', escKeyHandler);
}

export function stopEscapeListener() {
    if (escKeyHandler) {
        document.removeEventListener('keydown', escKeyHandler);
        escKeyHandler = null;
    }
}

export async function dispose() {
    if (engine) {
        engine = null;
    }
}
