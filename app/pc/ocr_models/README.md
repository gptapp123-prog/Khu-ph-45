# Vietnamese OCR model

`vie.traineddata` is the Vietnamese model from the official [tesseract-ocr/tessdata_fast](https://github.com/tesseract-ocr/tessdata_fast) project (Apache License 2.0; see `LICENSE`).

Source: `https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/main/vie.traineddata`
SHA-256: `79df64caf7bcfb2a27df5042ecb6121e196eada34da774956995747636d5bfa1`

The model is bundled with the PC program and is read locally. Tesseract executable must still be installed separately on the PC; the app does not upload images to a remote OCR service. OCR values are suggestions until reviewed against the source image.

`eng.traineddata` is bundled from the same official project so Tesseract can run Vietnamese and English recognition together without depending on the PC's system tessdata directory.
Source: `https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/main/eng.traineddata`
SHA-256: `7d4322bd2a7749724879683fc3912cb542f19906c83bcc1a52132556427170b2`

The English model is stored as three XZ archive parts to keep each repository blob small. At runtime the app verifies the archive hash, decompresses and verifies the model hash, and writes both OCR models to a temporary local directory for Tesseract. The temporary directory contains only model binaries, never document data.
XZ archive SHA-256: `6e287ef6ff5dac5ef95295d76f685b6695ac57a6adcdbc23956627594ffdcc5c`
