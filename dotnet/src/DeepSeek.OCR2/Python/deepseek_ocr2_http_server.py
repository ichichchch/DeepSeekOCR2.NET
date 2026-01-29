import argparse
import base64
import json
import os
import tempfile
import time
import traceback
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer


_TOKENIZER = None
_MODEL = None


def _canonical_device(device: str | None) -> str:
    if not device:
        return "cpu"
    d = str(device).strip().lower()
    if d.startswith("cuda"):
        return "cuda"
    if d.startswith("cpu"):
        return "cpu"
    return d


def _patch_torch_device_redirection(torch, target_device: str, force_dtype=None):
    target = torch.device(target_device)

    tensor_cuda = getattr(torch.Tensor, "cuda", None)
    module_cuda = getattr(torch.nn.Module, "cuda", None)
    tensor_to = getattr(torch.Tensor, "to", None)
    module_to = getattr(torch.nn.Module, "to", None)
    tensor_half = getattr(torch.Tensor, "half", None)
    module_half = getattr(torch.nn.Module, "half", None)
    tensor_bfloat16 = getattr(torch.Tensor, "bfloat16", None)
    module_bfloat16 = getattr(torch.nn.Module, "bfloat16", None)

    def _rewrite_dtype(d):
        if force_dtype is None:
            return d
        if d in (getattr(torch, "float16", None), getattr(torch, "bfloat16", None)):
            return force_dtype
        return d

    if tensor_cuda is not None and not hasattr(torch.Tensor.cuda, "_deepseek_ocr2_patched"):
        def _tensor_cuda(self, *args, **kwargs):
            return self.to(target)
        _tensor_cuda._deepseek_ocr2_patched = True
        _tensor_cuda._deepseek_ocr2_original = tensor_cuda
        torch.Tensor.cuda = _tensor_cuda

    if module_cuda is not None and not hasattr(torch.nn.Module.cuda, "_deepseek_ocr2_patched"):
        def _module_cuda(self, *args, **kwargs):
            return self.to(target)
        _module_cuda._deepseek_ocr2_patched = True
        _module_cuda._deepseek_ocr2_original = module_cuda
        torch.nn.Module.cuda = _module_cuda

    if tensor_to is not None and not hasattr(torch.Tensor.to, "_deepseek_ocr2_patched"):
        def _tensor_to(self, *args, **kwargs):
            if target.type == "cpu" and args:
                a0 = args[0]
                if isinstance(a0, str) and a0.lower().startswith("cuda"):
                    args = (target,) + tuple(args[1:])
                elif isinstance(a0, torch.device) and a0.type == "cuda":
                    args = (target,) + tuple(args[1:])
                elif isinstance(a0, torch.dtype):
                    args = (_rewrite_dtype(a0),) + tuple(args[1:])
                elif len(args) >= 2 and isinstance(args[1], torch.dtype):
                    args = (args[0], _rewrite_dtype(args[1])) + tuple(args[2:])
            if target.type == "cpu" and "device" in kwargs:
                d = kwargs.get("device")
                if isinstance(d, str) and d.lower().startswith("cuda"):
                    kwargs["device"] = target
                elif isinstance(d, torch.device) and d.type == "cuda":
                    kwargs["device"] = target
            if target.type == "cpu" and "dtype" in kwargs:
                kwargs["dtype"] = _rewrite_dtype(kwargs.get("dtype"))
            return tensor_to(self, *args, **kwargs)
        _tensor_to._deepseek_ocr2_patched = True
        _tensor_to._deepseek_ocr2_original = tensor_to
        torch.Tensor.to = _tensor_to

    if module_to is not None and not hasattr(torch.nn.Module.to, "_deepseek_ocr2_patched"):
        def _module_to(self, *args, **kwargs):
            if target.type == "cpu" and args:
                a0 = args[0]
                if isinstance(a0, str) and a0.lower().startswith("cuda"):
                    args = (target,) + tuple(args[1:])
                elif isinstance(a0, torch.device) and a0.type == "cuda":
                    args = (target,) + tuple(args[1:])
                elif isinstance(a0, torch.dtype):
                    args = (_rewrite_dtype(a0),) + tuple(args[1:])
                elif len(args) >= 2 and isinstance(args[1], torch.dtype):
                    args = (args[0], _rewrite_dtype(args[1])) + tuple(args[2:])
            if target.type == "cpu" and "device" in kwargs:
                d = kwargs.get("device")
                if isinstance(d, str) and d.lower().startswith("cuda"):
                    kwargs["device"] = target
                elif isinstance(d, torch.device) and d.type == "cuda":
                    kwargs["device"] = target
            if target.type == "cpu" and "dtype" in kwargs:
                kwargs["dtype"] = _rewrite_dtype(kwargs.get("dtype"))
            return module_to(self, *args, **kwargs)
        _module_to._deepseek_ocr2_patched = True
        _module_to._deepseek_ocr2_original = module_to
        torch.nn.Module.to = _module_to

    if tensor_half is not None and not hasattr(torch.Tensor.half, "_deepseek_ocr2_patched"):
        def _tensor_half(self, *args, **kwargs):
            if target.type == "cpu" and force_dtype is not None:
                return self.to(dtype=force_dtype)
            return tensor_half(self, *args, **kwargs)
        _tensor_half._deepseek_ocr2_patched = True
        _tensor_half._deepseek_ocr2_original = tensor_half
        torch.Tensor.half = _tensor_half

    if module_half is not None and not hasattr(torch.nn.Module.half, "_deepseek_ocr2_patched"):
        def _module_half(self, *args, **kwargs):
            if target.type == "cpu" and force_dtype is not None:
                return self.to(dtype=force_dtype)
            return module_half(self, *args, **kwargs)
        _module_half._deepseek_ocr2_patched = True
        _module_half._deepseek_ocr2_original = module_half
        torch.nn.Module.half = _module_half

    if tensor_bfloat16 is not None and not hasattr(torch.Tensor.bfloat16, "_deepseek_ocr2_patched"):
        def _tensor_bfloat16(self, *args, **kwargs):
            if target.type == "cpu" and force_dtype is not None:
                return self.to(dtype=force_dtype)
            return tensor_bfloat16(self, *args, **kwargs)
        _tensor_bfloat16._deepseek_ocr2_patched = True
        _tensor_bfloat16._deepseek_ocr2_original = tensor_bfloat16
        torch.Tensor.bfloat16 = _tensor_bfloat16

    if module_bfloat16 is not None and not hasattr(torch.nn.Module.bfloat16, "_deepseek_ocr2_patched"):
        def _module_bfloat16(self, *args, **kwargs):
            if target.type == "cpu" and force_dtype is not None:
                return self.to(dtype=force_dtype)
            return module_bfloat16(self, *args, **kwargs)
        _module_bfloat16._deepseek_ocr2_patched = True
        _module_bfloat16._deepseek_ocr2_original = module_bfloat16
        torch.nn.Module.bfloat16 = _module_bfloat16


def _load_model(model_name: str, device: str, dtype: str, attn_impl: str | None):
    global _TOKENIZER, _MODEL
    if _TOKENIZER is not None and _MODEL is not None:
        return _TOKENIZER, _MODEL

    fake = os.getenv("DEEPSEEK_OCR2_FAKE", "").strip().lower()
    if fake in ("1", "true", "yes", "on"):
        class _FakeTokenizer:
            pass

        class _FakeModel:
            def eval(self):
                return self

            def cuda(self, *args, **kwargs):
                return self

            def cpu(self, *args, **kwargs):
                return self

            def to(self, *args, **kwargs):
                return self

            def infer(self, tokenizer, prompt="", image_file="", output_path="", base_size=1024, image_size=768, crop_mode=True, save_results=False, **kwargs):
                return {"text": prompt or "<image>\nFree OCR."}

        _TOKENIZER = _FakeTokenizer()
        _MODEL = _FakeModel()
        return _TOKENIZER, _MODEL

    from transformers import AutoModel, AutoTokenizer
    import torch
    device = _canonical_device(device)
    if device == "cuda" and not torch.cuda.is_available():
        device = "cpu"
    if device != "cuda":
        os.environ.setdefault("CUDA_VISIBLE_DEVICES", "")
        _patch_torch_device_redirection(torch, "cpu", force_dtype=torch.float32)

    _TOKENIZER = AutoTokenizer.from_pretrained(model_name, trust_remote_code=True)

    model_kwargs = {
        "trust_remote_code": True,
        "use_safetensors": True,
    }
    if attn_impl:
        model_kwargs["_attn_implementation"] = attn_impl

    try:
        _MODEL = AutoModel.from_pretrained(model_name, **model_kwargs)
    except Exception:
        model_kwargs.pop("_attn_implementation", None)
        _MODEL = AutoModel.from_pretrained(model_name, **model_kwargs)

    _MODEL = _MODEL.eval()

    if device == "cuda":
        _MODEL = _MODEL.cuda()
        if dtype == "bfloat16":
            _MODEL = _MODEL.to(torch.bfloat16)
        elif dtype == "float16":
            _MODEL = _MODEL.to(torch.float16)
        else:
            _MODEL = _MODEL.to(torch.float32)
    else:
        _MODEL = _MODEL.cpu()
        _MODEL = _MODEL.to(torch.float32)

    return _TOKENIZER, _MODEL


def _read_json_body(handler: BaseHTTPRequestHandler):
    content_length = int(handler.headers.get("Content-Length", "0"))
    if content_length <= 0:
        return None
    raw = handler.rfile.read(content_length)
    return json.loads(raw.decode("utf-8"))


def _send_json(handler: BaseHTTPRequestHandler, status: int, payload: dict):
    raw = json.dumps(payload, ensure_ascii=False).encode("utf-8")
    handler.send_response(status)
    handler.send_header("Content-Type", "application/json; charset=utf-8")
    handler.send_header("Content-Length", str(len(raw)))
    handler.end_headers()
    handler.wfile.write(raw)


class _Handler(BaseHTTPRequestHandler):
    def do_GET(self):
        if self.path == "/health":
            _send_json(self, 200, {"ok": True})
            return
        _send_json(self, 404, {"error": "not found"})

    def do_POST(self):
        if self.path != "/ocr":
            _send_json(self, 404, {"error": "not found"})
            return

        try:
            body = _read_json_body(self)
            if body is None:
                _send_json(self, 400, {"error": "empty body"})
                return

            image_base64 = body.get("image_base64")
            if not image_base64:
                _send_json(self, 400, {"error": "image_base64 is required"})
                return

            prompt = body.get("prompt") or "<image>\nFree OCR."
            output_dir = body.get("output_dir")
            if output_dir:
                os.makedirs(output_dir, exist_ok=True)
            else:
                output_dir = tempfile.mkdtemp(prefix="deepseek-ocr2-")

            base_size = int(body.get("base_size", 1024))
            image_size = int(body.get("image_size", 768))
            crop_mode = bool(body.get("crop_mode", True))
            save_results = bool(body.get("save_results", False))

            tokenizer, model = _load_model(
                self.server.model_name,
                self.server.device,
                self.server.dtype,
                self.server.attn_impl,
            )

            image_bytes = base64.b64decode(image_base64)
            with tempfile.NamedTemporaryFile(suffix=".png", delete=False) as tmp:
                tmp.write(image_bytes)
                image_path = tmp.name

            t0 = time.time()
            res = model.infer(
                tokenizer,
                prompt=prompt,
                image_file=image_path,
                output_path=output_dir,
                base_size=base_size,
                image_size=image_size,
                crop_mode=crop_mode,
                save_results=save_results,
            )
            elapsed_ms = int((time.time() - t0) * 1000)

            try:
                os.unlink(image_path)
            except Exception:
                pass

            text = None
            if isinstance(res, dict):
                text = res.get("text") or res.get("result") or res.get("markdown")
            if text is None:
                text = str(res)

            files = []
            for name in ["result.mmd", "result_ori.mmd", "result_with_boxes.jpg"]:
                candidate = os.path.join(output_dir, name)
                if os.path.exists(candidate):
                    files.append(name)

            _send_json(
                self,
                200,
                {
                    "text": text,
                    "output_dir": output_dir,
                    "files": files,
                    "elapsed_ms": elapsed_ms,
                },
            )
        except Exception as ex:
            tb = traceback.format_exc()
            if len(tb) > 6000:
                tb = tb[-6000:]
            _send_json(self, 500, {"error": str(ex), "traceback": tb})

    def log_message(self, format, *args):
        return


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8008)
    parser.add_argument("--model", default=os.getenv("DEEPSEEK_OCR2_MODEL", "deepseek-ai/DeepSeek-OCR-2"))
    parser.add_argument("--device", default=os.getenv("DEEPSEEK_OCR2_DEVICE", "cuda"))
    parser.add_argument("--dtype", default=os.getenv("DEEPSEEK_OCR2_DTYPE", "bfloat16"))
    parser.add_argument("--attn-impl", default=os.getenv("DEEPSEEK_OCR2_ATTN_IMPL", "flash_attention_2"))
    args = parser.parse_args()
    args.device = _canonical_device(args.device)

    server = ThreadingHTTPServer((args.host, args.port), _Handler)
    server.model_name = args.model
    server.device = args.device
    server.dtype = args.dtype
    server.attn_impl = args.attn_impl

    server.serve_forever()


if __name__ == "__main__":
    main()

