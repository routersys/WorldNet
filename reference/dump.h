#ifndef WORLDNET_REFERENCE_DUMP_H_
#define WORLDNET_REFERENCE_DUMP_H_

#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "world/fft.h"

extern char g_outdir[1024];

inline void WriteDump(const char *name, const int *dims, int ndim,
    const double *data) {
  char path[2048];
  snprintf(path, sizeof(path), "%s/%s.bin", g_outdir, name);
  FILE *fp = fopen(path, "wb");
  if (fp == NULL) {
    printf("cannot open %s\n", path);
    exit(1);
  }
  int32_t nd = ndim;
  fwrite(&nd, sizeof(int32_t), 1, fp);
  for (int i = 0; i < ndim; ++i) {
    int32_t d = dims[i];
    fwrite(&d, sizeof(int32_t), 1, fp);
  }
  size_t total = 1;
  for (int i = 0; i < ndim; ++i) total *= (size_t)dims[i];
  fwrite(data, sizeof(double), total, fp);
  fclose(fp);
}

inline void Write1D(const char *name, const double *data, int n) {
  int dims[1];
  dims[0] = n;
  WriteDump(name, dims, 1, data);
}

inline void Write2D(const char *name, const double *data, int rows, int cols) {
  int dims[2];
  dims[0] = rows;
  dims[1] = cols;
  WriteDump(name, dims, 2, data);
}

inline void WriteRows(const char *name, const double *const *rows_data,
    int rows, int cols) {
  double *flat = new double[(size_t)rows * (size_t)cols];
  for (int i = 0; i < rows; ++i)
    memcpy(flat + (size_t)i * (size_t)cols, rows_data[i],
        sizeof(double) * (size_t)cols);
  Write2D(name, flat, rows, cols);
  delete[] flat;
}

inline void WriteComplex(const char *name, const fft_complex *data, int n) {
  double *flat = new double[(size_t)n * 2];
  for (int i = 0; i < n; ++i) {
    flat[i * 2] = data[i][0];
    flat[i * 2 + 1] = data[i][1];
  }
  Write2D(name, flat, n, 2);
  delete[] flat;
}

inline void WriteInts(const char *name, const int *data, int n) {
  double *flat = new double[(size_t)n];
  for (int i = 0; i < n; ++i) flat[i] = (double)data[i];
  Write1D(name, flat, n);
  delete[] flat;
}

inline void WriteScalar(const char *name, double value) {
  Write1D(name, &value, 1);
}

#endif  // WORLDNET_REFERENCE_DUMP_H_
