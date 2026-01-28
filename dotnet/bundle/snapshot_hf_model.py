import argparse


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--model", required=True)
    parser.add_argument("--out", required=True)
    args = parser.parse_args()

    from huggingface_hub import snapshot_download

    snapshot_download(
        repo_id=args.model,
        local_dir=args.out,
        local_dir_use_symlinks=False,
        resume_download=True,
    )


if __name__ == "__main__":
    main()

