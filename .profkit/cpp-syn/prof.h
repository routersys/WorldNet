#pragma once
#include <chrono>
extern double g_prof[32];
static inline double Tn() { return std::chrono::duration<double, std::micro>(std::chrono::steady_clock::now().time_since_epoch()).count(); }
#define PT(i) do { double _n = Tn(); g_prof[i] += _n - _t; _t = _n; } while (0)
