# Publishing DART on GitHub

Repository: https://github.com/rhysnewman/DART

Commit source, installer scripts, tests and Markdown documentation. Keep `vendor`, portable distributions, installer payloads, compiled installers and runtime data out of the Git history. `.gitignore` excludes these generated/private locations. Review the staged file list and diff before each push; exclusion rules do not remove files already committed.

The DART source currently has no licence grant, by the owner's choice. Third-party components keep their own licences. Do not describe the complete project as MIT-licensed or public-domain software.

## Source update

After configuring Git authentication and a commit identity:

```powershell
git remote add origin https://github.com/rhysnewman/DART.git
# If origin already exists, inspect it before changing it.
git add README.md ONVIF.md VALIDATION.md .gitignore docs installer scripts src tests
git diff --cached --stat
git diff --cached
git commit -m "Document DART design, installation and Agent DVR testing"
git push -u origin HEAD
```

Use the existing remote branch if it already contains work. Do not force-push over an existing GitHub README or licence; integrate it first.

## Binary releases

The installer EXE is self-contained and about 110 MB. Keep it out of ordinary Git commits and use a GitHub Release asset instead. The `payload` directory is not needed by installer recipients. Preserve third-party notices and assemble the exact bundled FFmpeg/linked-library corresponding source/build materials or appropriate compliant source offer before external binary redistribution, as noted in `installer/THIRD-PARTY.txt`. Those materials are not currently assembled in this source repository. Publishing source documentation can proceed independently of publishing the binary installer.

For a release, record tested dependency versions, create a version tag, write release notes, attach the installer and SHA-256 checksum, and link the installation/test guides. Test the actual installer on a clean Windows PC before claiming clean-machine deployment support. Do not include camera.json, mediamtx.yml, logs, screen captures, passwords, private keys or other runtime data in release assets.
