#!/usr/bin/env python3

# Copyright (C) 2023 jmh
# SPDX-License-Identifier: GPL-3.0-only

import os
import argparse
import subprocess
import shutil
import contextlib
import xml.etree.ElementTree as ElementTree
from typing import Generator

REPO = "https://github.com/stratumauth/app.git"

FRAMEWORK = "net9.0-android"
CONFIGURATION = "Release"

PROJECT_NAMES = {
    "android": "Stratum.Droid",
    "wearos": "Stratum.WearOS",
}


def get_full_path(input: str) -> str:
    return os.path.abspath(os.path.expanduser(input))


@contextlib.contextmanager
def get_build_dir_path(output_path: str) -> Generator[str, None, None]:
    build_path = f"{output_path}.build"

    try:
        os.makedirs(build_path, exist_ok=True)
        yield build_path
    finally:
        shutil.rmtree(build_path)


def adjust_csproj(build_dir: str, args: argparse.Namespace):
    csproj_path = os.path.join(build_dir, args.project, f"{args.project}.csproj")
    csproj = ElementTree.parse(csproj_path)

    property_group = csproj.find("PropertyGroup")

    package_format = ElementTree.Element("AndroidPackageFormat")
    package_format.text = args.package
    property_group.append(package_format)

    package_per_abi = ElementTree.Element("AndroidCreatePackagePerAbi")
    package_per_abi.text = "true" if args.package == "apk" else "false"
    property_group.append(package_per_abi)

    if args.fdroid:
        define_constants = ElementTree.Element("DefineConstants")
        define_constants.text = "FDROID"
        property_group.append(define_constants)

    csproj.write(csproj_path, xml_declaration=True, encoding="utf-8")


def build_project(build_dir: str, args: argparse.Namespace):
    os.makedirs(args.output, exist_ok=True)
    build_args = [f"-f:{FRAMEWORK}", f"-c:{CONFIGURATION}"]

    if args.keystore is not None:
        build_args += [
            "-p:AndroidKeyStore=True",
            f'-p:AndroidSigningKeyStore="{args.keystore}"',
            f'-p:AndroidSigningStorePass="{args.keystore_pass}"',
            f'-p:AndroidSigningKeyAlias="{args.keystore_alias}"',
            f'-p:AndroidSigningKeyPass="{args.keystore_key_pass}"',
        ]

    project_file_path = os.path.join(build_dir, args.project, f"{args.project}.csproj")
    subprocess.run(["dotnet", "publish", *build_args, project_file_path], check=True)


def move_build_artifacts(args: argparse.Namespace, build_dir: str, output_dir: str):
    publish_dir = os.path.join(
        build_dir, args.project, "bin", CONFIGURATION, FRAMEWORK, "publish"
    )

    files = os.listdir(publish_dir)

    for file in filter(lambda f: "Signed" in f and f[-3:] == args.package, files):
        artifact_path = os.path.join(publish_dir, file)

        if args.project == PROJECT_NAMES["wearos"]:
            output_file = file.replace("-Signed", ".wearos")
        elif args.fdroid:
            output_file = file.replace("-Signed", ".fdroid")
        else:
            output_file = file.replace("-Signed", "")

        output_path = os.path.join(output_dir, output_file)
        shutil.copy(artifact_path, output_path)


def get_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build Stratum")
    parser.add_argument(
        "project",
        metavar="P",
        type=str,
        choices=PROJECT_NAMES.keys(),
        help="Project to build",
    )
    parser.add_argument(
        "package",
        metavar="T",
        type=str,
        choices=["apk", "aab"],
        help="Package type to build",
    )

    parser.add_argument(
        "--fdroid",
        action=argparse.BooleanOptionalAction,
        help="Build without proprietary libraries (Wear OS)",
    )
    parser.add_argument(
        "--output",
        type=str,
        help="Build output path (defaults to 'out')",
        default="out",
    )
    parser.add_argument(
        "--branch", type=str, help="Branch to build from (uses default if not set)"
    )

    signing = parser.add_argument_group("build signing")
    signing.add_argument(
        "--keystore",
        type=str,
        help="Keystore location (if not set, output is signed with debug keystore)",
    )
    signing.add_argument("--keystore-pass", type=str, help="Keystore password")
    signing.add_argument("--keystore-alias", type=str, help="Keystore alias")
    signing.add_argument("--keystore-key-pass", type=str, help="Keystore key password")

    args = parser.parse_args()

    if args.project == "wearos" and args.fdroid:
        raise ValueError("Cannot build Wear OS as F-Droid")

    if args.keystore is not None:
        if (
            args.keystore_pass is None
            or args.keystore_alias is None
            or args.keystore_key_pass is None
        ):
            raise ValueError(
                "Keystore provided but not all signing arguments are present"
            )

        args.keystore = get_full_path(args.keystore)

    args.output = get_full_path(args.output)
    args.project = PROJECT_NAMES[args.project]

    return args


def validate_path():
    for program in ["git", "dotnet"]:
        if shutil.which(program) is None:
            raise ValueError(f"Missing {program} on PATH")


def main():
    args = get_args()
    validate_path()

    print(f"Building {args.project} as {args.package}")

    with get_build_dir_path(args.output) as build_dir:
        git_command = ["git", "clone"]

        if args.branch is not None:
            git_command += ["-b", args.branch]

        git_command += [REPO, build_dir]
        subprocess.run(git_command, check=True)

        adjust_csproj(build_dir, args)
        build_project(build_dir, args)
        move_build_artifacts(args, build_dir, args.output)

    print("Build finished")


if __name__ == "__main__":
    main()
